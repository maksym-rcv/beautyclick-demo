using System;
using Dto.Entities;
using Site.Helper;
using Site.Helper.Map;
using Site.Models.Salon;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Migrations.Sql;
using System.Linq;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Mvc;
using Dto.Entities.Custom;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Shared.Geo;
using Site.Extentions;
using Site.Models;
using Site.Models.Home;
using Site.Models.Search;
using ViberSharedLogic;
using Exception = System.Exception;
using SalonServiceModel = Site.Models.Home.SalonServiceModel;
using Shared.Enums;
using System.Threading.Tasks;
using Site.SmsProvider;
using System.Net.Mail;

namespace Site.Controllers
{
    [CustomAuthorization(ActionValue = "Login", AreaValue = "", ControllerValue = "Home")]
    public class HomeController : BaseController
    {
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(HomeController));
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public HomeController()
        {
        }

        public HomeController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }
        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        [AllowAnonymous]
        public ActionResult Index(string id="")
        {
            if (!string.IsNullOrEmpty(id))
            {
                if(User?.Identity == null || User.Identity.IsAuthenticated == false) {
                    var userId = DB.UserDb.UserIdByHashLink(id);
                    if (userId > 0)
                    {
                        _log.Debug($"Quick login by hash {id} UserId {userId}");
                        var user = UserManager.FindById(userId);
                        if (user == null)
                        {
                            _log.Debug($"User not found for Quick login hash: {id}  userId: {userId}");
                        }
                        else
                        {
                            SignInManager.SignIn(user, true, true);
                            return RedirectToAction("MySchedule", new { id = 1 });
                        }
                    }
                }
            }

            var model = new HomeModel();
            model.CityId = UserCityId;
            model.CitiesDtos = CachedCities();
            model.Categories = DB.ServiceCategoryDb.GetCategories(false);
            var dbResult = DB.SalonDb.SearchSalonWithServices((long)UserCountry, null, null, 0, 0, null);
            model.SalonModel = new PartialSalonSearchViewModel();
            model.SalonModel.Salons = SalonDbToViewModel(dbResult, true);
            model.SalonModel.UrlWithPlaceHolder = "/Salon/id";
            model.SalonModel.Salons.Shuffle();
            model.ClientArticles = DB.ArticleDb.GetCustomerPages(5, true);
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Business()
        {
            return View();
        }
        [AllowAnonymous]
        public ActionResult About() { return View(); }

        [AllowAnonymous]
        public ActionResult Instrukcia() { return View(); }

        [AllowAnonymous]
        public ActionResult InfoAcena(short? id) {
            var retModel = new SalonModel();
            if (id == null)
            {
                retModel.ReferalId = 0;
            }
            else
            {
                retModel.ReferalId = id.Value;
            }
            return View(retModel); 
        }

        [AllowAnonymous]
        public ActionResult Otazky() { return View(); }

        [AllowAnonymous]
        public ActionResult Baliky() { return View(); }

        [AllowAnonymous]
        public ActionResult Dotaznik() { return View(); }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if(User?.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }


        [AllowAnonymous]
        public ActionResult Salon(long? id, long? service, string datestr)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index");
            }

            DateTime date = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(datestr))
            {
                if (DateTime.TryParse(datestr, out DateTime dt))
                {
                    date = dt;
                }
            }
            
            var salonDto = DB.SalonDb.GetSalonInfo(id.Value);

            SalonCustomerViewModel retModel = new SalonCustomerViewModel();
            
            if (salonDto == null)
            {
                return RedirectToAction("Index");
            }

            var first = salonDto.FirstOrDefault();
            if (first == null)
            {
                return RedirectToAction("Index");
            }

            retModel.Images = salonDto.DistinctBy(a => a.FullImgUrl).Where(a=>!string.IsNullOrEmpty(a.FullImgUrl)).Select(a => a.FullImgUrl).ToList();

            retModel.Name = first.Name;
            retModel.Address = first.Address;
            retModel.HowToFind = first.HowToFind;
            retModel.CityName = CityNameById(first.CityId);
            retModel.CityNameAlt = CityNameByAlt(first.CityId);
            retModel.CreatedUtc = first.CreatedUtc;
            retModel.Description = first.Description;
            retModel.Phone = first.Phone;
            retModel.Id = first.Id;
            retModel.MainPhoto = first.MainPhoto;
            retModel.Services=new List<SalonServiceModel>();
            retModel.Marks=new List<Marks>();
            retModel.MastersModel = new PartialSalonMasterModel();
            retModel.MastersModel.ServiceId = service;
            retModel.MastersModel.SalonId = id;
            retModel.BookSchedule= retModel.AvailableSlots = BookModelHelper.GenerateModelCustomer(0, 0, false,ClientBrowserUtcOffset,service);
            retModel.ServiceId = service;
            retModel.VideoLink = first.VideoLink;
            retModel.Date = date;
            retModel.Package = first.PackageId;
            //retModel.AvailableSlots= BookModelHelper.GenerateModelCustomer(0, 0, false);
            retModel.TimeBookModel = new TimeBookModel()
            {
                ServiceId = 0,
                AvailableHours = new Dictionary<short, List<TimeSpan>>(),
                ServiceName = "",
                Date = DateTime.MinValue,
                ServicePrice = 0,
                ServiceDuration = 0
            };
            retModel.ActiveTab = "about";
            retModel.IsFavorite = User.GetFavoriteSalons().Contains(retModel.Id);
            foreach (var mark in salonDto.Where(a=>a.FeedBackId.HasValue).DistinctBy(a=>a.FeedBackId))
            {
                retModel.Marks.Add(new Marks()
                {
                    IsAnonym = mark.IsAnonym,
                    FeedBackName = (mark.IsAnonym!=null && mark.IsAnonym.Value) ? "Zákazník" :  mark.FeedBackName,
                    FeedBackAvatar = mark.FeedBackAvatar,
                    FeedBackComment=mark.Comment,
                    FeedBackCreatedUtc = mark.MarkCreatedUtc,
                    FeedBackOwnerId = mark.FeedBackUserId??0,
                    Mark = mark.Mark,
                    MasterId = mark.FeedBackMasterId
                });
            }

            var salonServices = DB.SalonDb.GetSalonServices(id.Value);

            foreach (var s in salonServices.DistinctBy(a => a.ServiceId).OrderBy(b => b.SpecialityName))
            {
                retModel.Services.Add(new SalonServiceModel()
                {
                    Name = s.ServiceName,
                    ServiceDescription = s.ServiceDescription,
                    Price = s.Price??0,
                    ServiceId = s.ServiceId,
                    SpecialityName = s.SpecialityName,
                    Duration = s.Duration??0,
                    NameAlt = s.ServiceNameAlt
                });
            }

            retModel.MastersModel.Masters=new List<SalonMasterModel>();

            var mastersUnique = new List<Dto.Entities.Custom.MasterShortDto>();
            foreach (var master in salonDto.Where(a => a.MasterId.HasValue))
            {
                if (retModel.MastersModel.Masters.Exists(x => x.MasterId == master.MasterId))
                {
                    var index = retModel.MastersModel.Masters.FindIndex(x => x.MasterId == master.MasterId);
                    if (!retModel.MastersModel.Masters[index].SpecialityName.Contains(master.SpecialityName))
                    {
                        retModel.MastersModel.Masters[index].SpecialityName += ", " + master.SpecialityName;
                    }
                }
                else
                {
                    var mastersServices = salonDto.Where(a => a.MasterId == master.MasterId).Select(a => a.ServiceId).DistinctBy(a => a).ToList();
                    var markCount = salonDto.DistinctBy(a => a.FeedBackId).Count(a => a.FeedBackMasterId == master.MasterId && a.Mark.HasValue);
                    var markSum = salonDto.Where(a => a.FeedBackMasterId == master.MasterId).DistinctBy(a => a.FeedBackId).Sum(a => a.Mark);
                    var m = new SalonMasterModel()
                    {
                        MasterId = master.MasterId ?? 0,
                        MasterName = master.MasterName,
                        SpecialityName = master.SpecialityName,
                        MasterImage = master.MasterImage,
                        SpecialityNameAlt = master.SpecialityNameAlt,
                        ServiceId = service,
                        SalonName = first.Name
                    };
                    if ((markSum.HasValue && markSum > 0) && markCount > 0)
                    {
                        m.Mark = (short)Math.Round((decimal)(markSum / markCount));
                    }

                    if (service.HasValue && service.Value > 0)
                    {
                        if (mastersServices.Contains(service.Value))
                        {
                            retModel.MastersModel.Masters.Add(m);
                        }
                    }
                    else if (!service.HasValue || service.Value == 0)
                    {
                        retModel.MastersModel.Masters.Add(m);
                    }
                }
            }

            return View(retModel);
        }

        [AllowAnonymous]
        public ActionResult Master(long? id, long? service, string tab, DateTime? date)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(tab))
            {
                tab = "about";
            }

            if (date == null)
            {
                date = DateTime.UtcNow;
            }

            var masterWithSalonDto = DB.MasterDb.GetMasterWithSalonInfo(id.Value);

            MasterCustomerViewModel retModel = new MasterCustomerViewModel();

            if (masterWithSalonDto == null)
            {
                return RedirectToAction("Index");
            }

            var first = masterWithSalonDto.FirstOrDefault();
            if (first == null)
            {
                return RedirectToAction("Index");
            }

            retModel.Images = masterWithSalonDto.DistinctBy(a => a.ImageId).Where(a=>!string.IsNullOrEmpty(a.MasterWorkImage)).Select(a => a.MasterWorkImage).ToList();
            if (retModel.Images.Count == 0)
            {
                if (!string.IsNullOrEmpty(first.MainPhoto))
                {
                    retModel.Images.Add(first.MainPhoto);
                }

                if (!string.IsNullOrEmpty(first.AvatarUrl))
                {
                    retModel.Images.Add(first.AvatarUrl);
                }
            }
            retModel.MasterName = first.MasterName;
            retModel.SalonName = first.SalonName;
            retModel.SalonId = first.SalonId;
            retModel.Address = first.Address;
            retModel.CityName = first.CityName;
            retModel.Description = first.Description;
            retModel.MasterId = id.Value;
            retModel.MainPhoto = first.MainPhoto;
            retModel.Services = new List<SalonServiceModel>();
            retModel.Marks = new List<Marks>();
            retModel.BookSchedule= retModel.AvailableSlots = BookModelHelper.GenerateModelCustomer(0, 0, false,ClientBrowserUtcOffset,service, true,id??0);
            retModel.ServiceId = service;
            retModel.VideoLink = first.VideoLink;
            retModel.Date = date;
            retModel.AvatarUrl = first.AvatarUrl;
            retModel.TimeBookModel = new TimeBookModel()
            {
                ServiceId = 0,
                AvailableHours = new Dictionary<short, List<TimeSpan>>(),
                ServiceName = "",
                Date = DateTime.MinValue,
                ServicePrice = 0,
                ServiceDuration = 0
            };
            retModel.ActiveTab = tab;
            retModel.IsFavorite = User.GetFavoriteMaster().Contains(retModel.MasterId);
            foreach (var mark in masterWithSalonDto.Where(a => a.FeedBackId.HasValue).DistinctBy(a => a.FeedBackId))
            {
                retModel.Marks.Add(new Marks()
                {
                    IsAnonym = mark.IsAnonym,
                    FeedBackName = (mark.IsAnonym != null && mark.IsAnonym.Value) ? "Zákazník" : mark.FeedBackName,
                    FeedBackAvatar = mark.FeedBackAvatar,
                    FeedBackComment = mark.Comment,
                    FeedBackCreatedUtc = mark.MarkCreatedUtc,
                    FeedBackOwnerId = mark.FeedBackUserId ?? 0,
                    Mark = mark.Mark,
                    MasterId = mark.FeedBackMasterId
                });
            }

            var masterServices = DB.MasterDb.GetMasterServices(id.Value);
            var userSelService = (long?) Session["UserSelectedService"] ?? 0;

            foreach (var s in masterServices.DistinctBy(a => a.ServiceId))
            {
                retModel.Services.Add(new SalonServiceModel()
                {
                    Name = s.ServiceName,
                    ServiceDescription = s.ServiceDescription,
                    Price = s.ServicePrice ?? 0,
                    ServiceId = s.ServiceId ?? 0,
                    Duration = s.ServiceDuration ?? 0,
                    SpecialityName = s.MasterSpecialty
                });
            }

            if (userSelService > 0)
            {
                var selectedService = retModel.Services.FirstOrDefault(a => a.ServiceId == userSelService);
                if (selectedService != null)
                {
                    retModel.Services.Remove(selectedService);
                    retModel.Services.Insert(0, selectedService);
                }
            }
            return View(retModel);
        }


        public ActionResult Facebook()
        {
            var schedules = DB.ScheduleDb.GetCustomerSchedules(UserId);
            var userFavoriteSalons = User.GetFavoriteSalons();
            foreach (var s in schedules)
            {
                s.From = s.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                s.To = s.To.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                s.IsFavorite = userFavoriteSalons.Contains(s.SalonId);
            }
            var retModel = new CustomSchedulesViewModel();
            var now = DateTime.Now;
            var lst = schedules.Where(a => a.Date > now).OrderBy(a => a.Date > now).ToList();
            lst.AddRange(schedules.Where(a => lst.All(p2 => p2.BookedId != a.BookedId)).OrderByDescending(a => a.Date).ToList());
            retModel.Schedules = lst;
            retModel.IsMessagesDisabled = DB.UserDb.IsMessagesDisabled(UserId);
            retModel.CustomerTimeZoneShift = base.ClientBrowserUtcOffset;
            retModel.CurrentTime = DateTime.Now;
            retModel.BookSchedule = BookModelHelper.GenerateModel(0, 0, false, true);
            retModel.TimeBookModel = new TimeBookModel()
            {
                ServiceId = 0,
                AvailableHours = new Dictionary<short, List<TimeSpan>>(),
                ServiceName = "",
                Date = DateTime.MinValue,
                ServicePrice = 0,
                ServiceDuration = 0
            };

            return View(retModel);
        }

        public ActionResult MySchedule(string id)
        {
            var schedules = DB.ScheduleDb.GetCustomerSchedules(UserId);
            var userFavoriteSalons = User.GetFavoriteSalons();
            foreach (var s in schedules)
            {
                s.From = s.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                s.To = s.To.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                s.IsFavorite = userFavoriteSalons.Contains(s.SalonId);
            }
            var retModel = new CustomSchedulesViewModel();
            var now = DateTime.Now;
            var lst = schedules.Where(a => a.Date > now).OrderBy(a => a.Date > now).ToList();
            lst.AddRange(schedules.Where(a => lst.All(p2 => p2.BookedId != a.BookedId)).OrderByDescending(a=>a.Date).ToList());
            retModel.Schedules = lst;
            retModel.IsMessagesDisabled = DB.UserDb.IsMessagesDisabled(UserId);
            retModel.CustomerTimeZoneShift = base.ClientBrowserUtcOffset;
            retModel.CurrentTime = DateTime.Now;
            retModel.BookSchedule = BookModelHelper.GenerateModel(0, 0, false, true);
            //BookModelHelper.GenerateModelCustomer(0, 0, false,ClientBrowserUtcOffset,service, true,id??0);
            retModel.TimeBookModel = new TimeBookModel()
            {
                ServiceId = 0,
                AvailableHours = new Dictionary<short, List<TimeSpan>>(),
                ServiceName = "",
                Date = DateTime.MinValue,
                ServicePrice = 0,
                ServiceDuration = 0
            };

            // если пользователь зашёл по коду из СМС (его добавил мастер)
            if(id == "1")
            {
                if (!DB.UserDb.IsActive(UserId) || retModel.IsMessagesDisabled) // проверим, если [IsActive]==null или [IsConfirmNeed]==1
                {
                    ViewData["Message"] = "Ask user for sending messages";
                }
                else
                {
                    ViewData["Message"] = "New user by master";
                }
            }
            return View(retModel);
        }

        [HttpGet]
        public ActionResult GetMySchedules()
        {
            var schedules = DB.ScheduleDb.GetCustomerSchedules(UserId);
            foreach (var s in schedules)
            {
                s.From = s.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                s.To = s.To.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
            }
            var now = DateTime.Now;
            var lst = schedules.Where(a => a.Date > now).OrderBy(a => a.Date > now).ToList();

            lst.AddRange(schedules.Where(a => lst.All(p2 => p2.BookedId != a.BookedId)).OrderByDescending(a => a.Date).ToList());
            //retModel.Schedules = notHappens;

            var retModel = new CustomSchedulesViewModel
            {
                Schedules = lst,
                CustomerTimeZoneShift = base.ClientBrowserUtcOffset,
                BookSchedule = BookModelHelper.GenerateModel(0, 0, false, true),
                TimeBookModel = new TimeBookModel()
                {
                    ServiceId = 0,
                    AvailableHours = new Dictionary<short, List<TimeSpan>>(),
                    ServiceName = "",
                    Date = DateTime.MinValue,
                    ServicePrice = 0,
                    ServiceDuration = 0
                }
            };
            return PartialView("_MySchedule", retModel);
        }

        [Authorize]
        [HttpPost]
        public ActionResult CancelBooking(long scheduleid,long masterid)
        {
            var retResult = new BookingResultModel();
            try
            {
                var book = DB.BookingDb.GetBookedForCustomer(scheduleid, UserId);
                if (book == null)
                {
                    _log.Error($"Someone tried to cancel not existed booking book id {scheduleid} masterid {masterid} userId {UserId}"); 
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                    return Json(retResult, JsonRequestBehavior.DenyGet);
                }

                DB.ScheduleDb.CustomerCancelSchedule(scheduleid, UserId);
                DB.BookingDb.RemoveBookRemind(scheduleid, UserId);
                var userInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(UserId);
                var masterViber = DB.MasterDb.GetMasterViber(masterid);
                var masterFacebook = DB.UserDb.GetUserFacebook(masterid);
                string strBookConfirmed = book.IsConfirmed ? "" : "nepotvrdenú";

                if (!string.IsNullOrEmpty(masterFacebook)) // отправка сообщения на Messenger Facebook мастеру
                {
                    var FbBot = new FacebookBotController();
                    string token = ConfigurationManager.AppSettings["FbPageToken"];
                    string message = $"Klient {userInfo.Name} (t.č.: {userInfo.PhoneNumber}) zrušil {strBookConfirmed} rezerváciu na: {book.ServiceName}. Rezervácia bola {book.Date:dd.MM} o {book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}. Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                    FbBot.SendNotify(token, masterFacebook, message);
                    DB.MailingDb.AddUserSendMessage(masterid, 1, 0, 0, message, UserId, masterFacebook, null);
                }
                else if (!string.IsNullOrEmpty(masterViber))     // шлём сообщение мастеру на Viber
                {
                    try
                    {   // Viber
                        string message = $"Klient {userInfo.Name} (t.č.: {userInfo.PhoneNumber}) zrušil {strBookConfirmed} rezerváciu na: {book.ServiceName}. Rezervácia bola {book.Date:dd.MM} o {book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}";
                        var res = MessageHelper.SendMsgWithButton(message, masterViber, "Náhľad objednávok", "https://beautyclick.sk/Master/Calendar", 3);
                        
                        DB.MailingDb.AddUserSendMessage(masterid, 0, 1, 0, message, UserId, masterViber, null);

                        _log.Debug(res.Status != ErrorCode.Ok
                            ? $"Message about canceled book wasn't send to master: {masterid}, masterviber: {masterViber}, due {res.StatusMessage}"
                            : $"Notification about canceled booking successfully sent to Master: {masterid}, book: {scheduleid}");
                    }
                    catch (Exception exSend)
                    {
                        _log.Error($"Error during sending master information about canceled book, {masterid} bookid: {scheduleid}", exSend);
                    }
                }
                else // шлём СМС мастеру
                {
                    /*try
                    {
                        var masterInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(masterid);
                        var m = new MessageSendingHelper();
                        //string message = $"Klient zrušil rezerváciu {book.Date:dd.MM} o {book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}. Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                        string message = $"Klient {userInfo.Name} (t.č.: {userInfo.PhoneNumber}) zrušil {strBookConfirmed} rezerváciu {book.Date:dd.MM} o {book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}. Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                        // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!

                        var res = m.SendSms(masterInfo.PhoneNumber, message);
                        DB.MailingDb.AddUserSendMessage(masterid, 0, 1, message, null);
                        _log.Debug($"SMS about canceled booking successfully sent to master {masterid}, sms info: {res}");
                    }
                    catch (Exception exSend)
                    {
                        _log.Error($"Error during sending SMS about canceled book to master {masterid}, bookid: {scheduleid}", exSend);
                    }*/
                }

                _log.Info($"User {UserId} requested cancellation schedule {scheduleid}");
                retResult.IsSuccess = true;
                retResult.Message = "Dokončené!";
            }
            catch (Exception ex)
            {
                _log.Error($"Error during user cancellation schedule {scheduleid}  user id {UserId}",ex);
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retResult, JsonRequestBehavior.DenyGet);
        }

        [AllowAnonymous]
        public ActionResult SalonNearByMe(double lat, double lang)
        {
            Helper.GeoHelper geoHelper = new GeoHelper();

            CoordinateBoundaries cb = new CoordinateBoundaries(lat, lang, 10, DistanceUnit.Kilometers);

            var salons =DB.SalonDb.GetSalonByLocation(cb.MinLat, cb.MinLng, cb.MaxLat, cb.MaxLng);
            var retVal = new List<Site.Models.Salon.SalonViewModel>();
            var favSalons=User.GetFavoriteSalons();
            foreach (var s in salons)
            {
                retVal.Add(new SalonViewModel()
                {
                    SalonId = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    Image = s.MainPhoto,
                    IsFavorite = favSalons.Contains(s.Id)
                });
            }
            return PartialView("Salon/_SalonList", retVal);
        }
        
        [AllowAnonymous]
        public ActionResult SalonMasters(long salon, long service)
        {
            var salonDto = DB.SalonDb.GetSalonMasters(salon,service);
            var retModel= new PartialSalonMasterModel();
            retModel.Masters=new List<SalonMasterModel>();
            retModel.ServiceId = service;
            retModel.SalonId = salon;

            if (service > 0)
            {
                Session["UserSelectedService"] = service;
            }

            var first = salonDto.FirstOrDefault();
            if (first == null)
            {
                return PartialView("_SalonMasters", retModel);
            }

            foreach (var master in salonDto.Where(a => a.MasterId.HasValue).DistinctBy(a => a.MasterId))
            {
                var markCount = salonDto.Count(a => a.MasterId == master.MasterId && a.Mark.HasValue);

                var markSum= salonDto.Where(a => a.MasterId == master.MasterId).Sum(a => a.Mark);

                var m = new SalonMasterModel()
                {
                    MasterId = master.MasterId ?? 0,
                    MasterName = master.MasterName,
                    SpecialityName = master.SpecialityName,
                    MasterImage = master.MasterImage,
                    SpecialityNameAlt = master.SpecialityNameAlt,
                    ServiceId = service,
                    SalonName = first.Name
                };
                if ((markSum.HasValue && markSum > 0) && markCount > 0)
                {
                    m.Mark = (short) Math.Round((decimal) (markSum / markCount));
                }
                retModel.Masters.Add(m);
            }

            return PartialView("_SalonMasters", retModel);
        }

        [AllowAnonymous]
        public ActionResult SalonAutocomplete(string term,long? cityId)
        {
            if (cityId == null)
            {
                cityId = 0;
            }
            var results = DB.SalonDb.AutoCompleteServiceAndSalon(term,cityId);

            var retVal = new List<AutoCompleteModel>();
            foreach (var r in results)
            {
                var a = new AutoCompleteModel();
                switch (r.SearchResultType)
                {
                    case SearchResultType.Salon:
                        {
                            a.Name = r.Name;
                            a.Value = "/Salon/" + r.Id;
                            a.AddValue = "salon";
                            a.Id = r.Id.ToString();
                        }
                        break;
                    case SearchResultType.Service:
                        {
                            a.Name = r.Name;
                            a.Value = "/SalonByService/" + r.Id;
                            a.AddValue = "service";
                            a.Id = r.Id.ToString();
                        }
                        break;

                    case SearchResultType.Category:
                        {
                            a.Name = r.Name;
                            a.Value = "Search/?cat=" + r.Id + "&cityId=" + cityId;
                            a.AddValue = "category";
                            a.Id = r.Id.ToString();
                        }
                        break;
                }
                var exist = retVal.FirstOrDefault(f => f.Value == a.Value);
                if (exist == null)
                {
                    retVal.Add(a);
                }
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }

        [AllowAnonymous]
        public ActionResult ClientNameAutocomplete(string term, long? salonId)
        {
            if (salonId == null)
            {
                salonId = 0;
            }
            var results = DB.SalonDb.AutoCompleteClientName(term, salonId);

            var retVal = new List<AutoCompleteModel>();
            foreach (var r in results)
            {
                var a = new AutoCompleteModel();
                a.Name = r.NameForMaster ?? r.Name;
                a.Value = r.PhoneNumber;
                a.Id = r.Id.ToString();

                var exist = retVal.FirstOrDefault(f => f.Value == a.Value);
                if (exist == null)
                {
                    retVal.Add(a);
                }
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [AllowAnonymous]
        public ActionResult GetCalendarForChangeBooking(long bookId, int month, int year, bool isToday = false, long master = 0, long service = 0, int day = 0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_CalendarForChangeBookingPopup", BookModelHelper.GenerateModelForChangeByMaster(bookId, month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }

        [AllowAnonymous]
        public ActionResult GetCalendarForMoveByMaster(long bookId, int month, int year, bool isToday = false, long master = 0, long service = 0, int day = 0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_CalendarForMoveByMasterPopup", BookModelHelper.GenerateModelForMoveByMaster(bookId, month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }

        [AllowAnonymous]
        public ActionResult GetCalendarForMoveByClient(long bookId, int month, int year, bool isToday = false, long master = 0, long service = 0, int day = 0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_CalendarForMoveByClientPopup", BookModelHelper.GenerateModelForMoveByMaster(bookId, month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }


        [AllowAnonymous]
        public ActionResult GetNewBookingByMasterCalendar(int month, int year, bool isToday = false, long master = 0, long service = 0, int day = 0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_NewBookingByMasterPopup", BookModelHelper.GenerateModelMaster(month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }

        [AllowAnonymous]
        public ActionResult GetBookCalendarMaster(int month, int year, bool isToday = false, long master = 0, long service = 0, int day = 0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_SalonMasterBookingPopup", BookModelHelper.GenerateModelMaster(month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }

        [AllowAnonymous]
        public ActionResult GetBookCalendar(int month, int year,bool isToday=false,long master=0,long service=0,int day=0)
        {
            bool isMasterCal = master > 0;
            return PartialView("_BookingPopup", BookModelHelper.GenerateModelCustomer(month, year, isMasterCal, ClientBrowserUtcOffset, service, isToday, master, day));
        }

        [AllowAnonymous]
        public ActionResult Pages()
        {
            var lst = DB.ArticleDb.GetCustomerPages();
            return View(lst);
        }

        [AllowAnonymous]
        public ActionResult Page(long? id)
        {
            if (id == null)
            {
                return RedirectToAction("pages");
            }
            var art=DB.ArticleDb.GetCustomerPage(id.Value);
            var model = new Site.Models.Article.ArticleDetailWithMore()
            {
                FullText = art.FullText,
                Image = art.Image,
                Name = art.Name,
                Title = art.Title,
                Views = art.Views,
                AddedUtc = art.AddedUtc,
                Articles = DB.ArticleDb.GetCustomerPages(10,true,id.Value)
            };
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult GetSalonFreeSlots(long ser, long sal, DateTime date,long master=0)
        {
            var dbModel = DBManager.Schedule.ScheduleDbManager.Instance.GetSalonMastersBookedByService(sal, ser, date);

            var retModel = new Models.Home.TimeBookModel();
            List<TimeSpan> openSlotFrom= new List<TimeSpan>();
            if (dbModel.Any())
            {
                var first = dbModel.FirstOrDefault();
                if (first != null)
                {
                    var from = first.ScheduleFrom;
                    var to = first.ScheduleTo;
                    int halfHourSlot = (to.Hours - from.Hours) * 4;
                    int serviceDuration = first.ServiceDuration;
                    var booked = dbModel.Where(a => a.BookedFrom >= from && a.BookedTo <= to).ToList();
                    for (int i = 0; i <= halfHourSlot; i++)
                    {
                        var startTime = from.Add(TimeSpan.FromMinutes(15 * i));
                        var endTime = startTime.Add(TimeSpan.FromMinutes(serviceDuration));
                        var isBooked = booked.FirstOrDefault(a => a.BookedFrom < endTime && a.BookedTo > startTime) !=null;
                        if (!isBooked && endTime<=to)
                        {
                            openSlotFrom.Add(startTime.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)));
                        }
                    }

                    retModel.ServiceId = ser;
                    retModel.ServiceDuration = first.ServiceDuration;
                    retModel.ServiceName = first.ServiceName;
                    retModel.ServicePrice = first.ServicePrice;
                    retModel.Date = date;
                    retModel.AvailableHours = new Dictionary<short, List<TimeSpan>>()
                    {
                        {1, openSlotFrom.Where(a=>a.Hours<=13).ToList()},
                        {2, openSlotFrom.Where(a=>a.Hours>13).ToList()}
                    };
                }
            }
            return PartialView("_BookTime", retModel);
        }

        [AllowAnonymous]
        public JsonResult GetMasterFreeSlotsNew(long ser, long master, string date, long bookId = 0)    // получаем список слотов в этом дне
        {
            var dts= date.Split('-');
            var dm = int.Parse(dts[1]);
            var dd = int.Parse(dts[2]);
            var dy = int.Parse(dts[0]);
            var daysInMo = DateTime.DaysInMonth(dy, dm);
            var dateTime = new DateTime(dy, dm, 1);
            if (dd> daysInMo)
            {
                dateTime=new DateTime(dy,dm,daysInMo);
            }
            else
            {
                dateTime = new DateTime(dy, dm, dd);
            }

            var dbModel = DBManager.Schedule.ScheduleDbManager.Instance.GetMasterTimeSlotsForMove(master, ser, dateTime, bookId);

            var retModel = new JsonResultModel();
            List<TimeSpan> openSlotFrom = new List<TimeSpan>();
            List<bool> slotsBookedState = new List<bool>();
            int slotIndex = 0;
            bool isRecommended = true;
            bool isAny = false; // есть ли хотя бы 1 свободный слот в этом дне
            var today = DateTime.UtcNow;
            var isToday = dateTime.Date == today.AddHours(ClientBrowserUtcOffset).Date;

            if (dbModel.Any())
            {
                var first = dbModel.FirstOrDefault();
                if (first != null)
                {
                    var from = first.ScheduleFrom;
                    var to = first.ScheduleTo;
                    int halfHourSlot = (to.Hours - from.Hours) * 4;
                    int serviceDuration = first.ServiceDuration;
                    var booked = dbModel.Where(a => a.BookedFrom >= from && a.BookedTo <= to).ToList();
                    if (first.ScheduleFrom > first.BookedFrom && first.ScheduleFrom < first.BookedTo)
                    {
                        //boooked wholeday
                    }
                    else
                    {
                        for (int i = 0; i <= halfHourSlot; i++)
                        {
                            var startTime = from.Add(TimeSpan.FromMinutes(15 * i));
                            var endTime = startTime.Add(TimeSpan.FromMinutes(serviceDuration));

                            bool isSlotFree = false;
                            var BooksOnTheWay = booked.FindAll(a => a.BookedFrom < endTime && a.BookedTo > startTime); // список мешающих записей
                            
                            if(endTime <= to)
                            {
                                if (BooksOnTheWay.Count() == 0)
                                {
                                    isSlotFree = true;
                                }
                                else
                                {
                                    if(BooksOnTheWay.Count() == 1 && BooksOnTheWay.FirstOrDefault().isOff 
                                        && !startTime.Equals(BooksOnTheWay.FirstOrDefault().BookedFrom)
                                        && endTime.Equals(BooksOnTheWay.FirstOrDefault().BookedFrom.Add(TimeSpan.FromMinutes(15)))) // слот является перерывом и начинается за 15 мин до окончания процедуры
                                    {
                                        isSlotFree = true;  // c учётом забора 15 мин от перерыва
                                    }
                                    else
                                    {
                                        if (isToday && startTime < today.TimeOfDay)
                                        {
                                            continue;
                                        }

                                        slotsBookedState.Add(true);
                                        openSlotFrom.Add(startTime.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)));
                                    }
                                }
                            }

                            if (isSlotFree)
                            {
                                if (isToday && startTime < today.TimeOfDay)
                                {
                                    continue;
                                }

                                slotsBookedState.Add(false);
                                openSlotFrom.Add(startTime.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)));
                                isAny = true;
                            }
                        }
                    }
                }
            }
            var  models= new Dictionary<short, List<TimeSpan>>()
            {
                {1, openSlotFrom.Where(a=>a.Hours<=13).ToList()},
                {2, openSlotFrom.Where(a=>a.Hours>=14).ToList()},
            };
            retModel.Html = "";
            string am = "";
            string pm = "";
            string legenda = "";
            if (isAny)
            {
                if (models.ContainsKey(1))
                {
                    string h = "";
                    foreach (var m in models[1])
                    {
                        if(slotsBookedState[slotIndex])
                        {
                            h += $"<li><span style=\"width:65px; height:35px; border-radius:10px; display:inline-block; font-size:18px; line-height: inherit; font-weight:600; text-align:center; background:#ff7c7c;padding:8px;\">{m.ToString(@"hh\:mm")}</span></li>";
                            isRecommended = true;
                        }
                        else
                        {
                            if (isRecommended)
                            {
                                h += $"<li class=\"timeSel\" data-time=\"{m.ToString(@"hh\:mm")}\"><button class=\"timeSelBtn recommended\">{m.ToString(@"hh\:mm")}</button></li>";
                                isRecommended = false;
                            }
                            else
                            {
                                h += $"<li class=\"timeSel\" data-time=\"{m.ToString(@"hh\:mm")}\"><button class=\"timeSelBtn\">{m.ToString(@"hh\:mm")}</button></li>";
                            }
                        }
                        slotIndex++;
                    }

                    am = "<div class=\"time-am\" id=\"time-am\"> " +
                         "<span>Dopoludnia</span>" +
                         " <ul class=\"hours\"> " +
                         h +
                         "</ul>" +
                         "</div>";
                }

                if (models.ContainsKey(2))
                {
                    string h = "";
                    foreach (var m in models[2])
                    {
                        if (slotsBookedState[slotIndex])
                        {
                            h += $"<li><span style=\"width:65px; height:35px; border-radius:10px; display:inline-block; font-size:18px; line-height: inherit; font-weight:600; text-align:center; background:#ff7c7c;padding:8px;\">{m.ToString(@"hh\:mm")}</span></li>";
                            isRecommended = true;
                        }
                        else
                        {
                            if (isRecommended)
                            {
                                h += $"<li class=\"timeSel\" data-time=\"{m.ToString(@"hh\:mm")}\"><button class=\"timeSelBtn recommended\">{m.ToString(@"hh\:mm")}</button></li>";
                                isRecommended = false;
                            }
                            else
                            {
                                h += $"<li class=\"timeSel\" data-time=\"{m.ToString(@"hh\:mm")}\"><button class=\"timeSelBtn\">{m.ToString(@"hh\:mm")}</button></li>";
                            }
                        }
                        slotIndex++;
                    }

                    pm = "<div class=\"time-pm\" id=\"time-pm\"> " +
                         "<span>Popoludní</span>" +
                         " <ul class=\"hours\"> " +
                         h +
                         "</ul>" +
                         "</div>";
                }
                legenda = "<div class=\"legenda\" style=\"margin-top:15px\">" +
                    "<h5><div style=\"float:left; width:25px\"><span style=\"background-color:#19d949;border-radius:30px;padding:0px 6px;margin-right:4px\"> </span> -</div>" +
                        "<div style=\"float:left;width:calc(100% - 25px);margin-bottom:5px\">voľný (odporúčame vybrať, ušetríte tak odborníkovi pracovný čas)</div></h5>"+
                    "<h5><div style=\"float:left; width:25px\"><span style=\"background-color:#C2F7BD;border-radius:30px;padding:0px 6px;margin-right:4px\"> </span> -</div>voľný</h5>" +
                    "<h5><div style=\"float:left; width:25px\"><span style=\"background-color:#ff7c7c;border-radius:30px;padding:0px 6px;margin-right:4px\"> </span> -</div>obsadený</h5>" +
                    "</div>";
            }
            else
            {
                am = "<div class=\"time-am\" id=\"time-am\"><span style=\"color:red;line-height:1.8em;text-align:center\">Tento dátum nie je voľný.<br />Vyberte si prosím iný.</span></div>";
            }

            retModel.Html = am + pm + legenda;

            return Json(retModel, JsonRequestBehavior.AllowGet);
        }


        [AllowAnonymous]
        public ActionResult GetMasterFreeSlots(long ser, long master, DateTime date)
        {
            var dbModel = DBManager.Schedule.ScheduleDbManager.Instance.GetMasterTimeSlots(master, ser, date);

            var retModel = new Models.Home.TimeBookModel();
            List<TimeSpan> openSlotFrom = new List<TimeSpan>();
            var today = DateTime.UtcNow;
            var isToday = date.Date == today.AddHours(ClientBrowserUtcOffset).Date;
            
            if (dbModel.Any())
            {
                var first = dbModel.FirstOrDefault();
                if (first != null)
                {
                    var from = first.ScheduleFrom; ;
                    var to = first.ScheduleTo;
                    int halfHourSlot = (to.Hours - from.Hours) * 4;
                    int serviceDuration = first.ServiceDuration;
                    var booked = dbModel.Where(a => a.BookedFrom >= from && a.BookedTo <= to).ToList();
                    for (int i = 0; i <= halfHourSlot; i++)
                    {
                        var startTime = from.Add(TimeSpan.FromMinutes(15 * i));
                        var endTime = startTime.Add(TimeSpan.FromMinutes(serviceDuration));
                        var isBooked = booked.FirstOrDefault(a => a.BookedFrom < endTime && a.BookedTo > startTime) != null;
                        if (!isBooked && endTime <= to)
                        {
                            if (isToday && startTime < today.TimeOfDay)
                            {
                                continue;
                            }

                            openSlotFrom.Add(startTime.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)));
                        }
                    }

                    retModel.ServiceId = ser;
                    retModel.ServiceDuration = first.ServiceDuration;
                    retModel.ServiceName = first.ServiceName;
                    retModel.ServicePrice = first.ServicePrice;
                }
            }
            retModel.AvailableHours = new Dictionary<short, List<TimeSpan>>()
            {
                {1, openSlotFrom.Where(a=>a.Hours<=13).ToList()},
                {2, openSlotFrom.Where(a=>a.Hours>14).ToList()},
            };
            retModel.Date = date;
            return PartialView("_BookTime", retModel);
        }

        [AllowAnonymous]
        //[Route("{search}/{cityId?}/{lat?}/{lng?}/{service?}")]
        public ActionResult Search(string search="",long? service = 0,long? city=0,long? cat = 0, double? lat=0, double? lng=0, DateTime? date=null,bool now=false )
        {
            GeoBound gb = null;
            if (lat.HasValue && lat > 0 && (lng.HasValue && lng > 0))
            {
                CoordinateBoundaries cb = null;
                try
                {
                    cb = new CoordinateBoundaries(lat.Value, lng.Value, 10, DistanceUnit.Kilometers);
                    gb = new GeoBound()
                    {
                        MaxLat = cb.MaxLat,
                        MinLat = cb.MinLat,

                        MinLng = cb.MinLng,
                        MaxLng = cb.MaxLng
                    };
                }
                catch (Exception ex)
                {
                    _log.Error("Error during searching process", ex);
                }
            }
            if (now)
            {
                date = DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
            }

            var searchResult = new SearchSalonResultWithServiceModel();
            searchResult.CityId = (city==null || city.Value==0) ? UserCityId : city.Value;
            searchResult.SalonModel = new PartialSalonSearchViewModel();
            searchResult.BookSchedule= BookModelHelper.GenerateModelCustomer(0, 0, false, ClientBrowserUtcOffset,service, true,0);
            searchResult.SearchServiceId = service;
            searchResult.CategoryId = cat;
            searchResult.Now = now;
            searchResult.SearchLat = lat;
            searchResult.SearchLng = lng;
            searchResult.CitiesDtos = CachedCities();
            var dbResult = DB.SalonDb.SearchSalonWithServices((long)UserCountry, city, gb, service, cat, date);
            searchResult.SalonModel.Salons = SalonDbToViewModel(dbResult,true);

            string dtString = date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "";
            searchResult.SalonModel.UrlWithPlaceHolder = $"/Salon/id/{service ?? 0}/{dtString}";
            
            if (service.HasValue && service.Value > 0)
            {
                searchResult.SearchServiceName = dbResult.FirstOrDefault()?.ServiceName;
            }

            if (searchResult.SalonModel.Salons == null || searchResult.SalonModel.Salons.Count <= 5)
            {
                // Add search for other cities
                searchResult.SalonModelMoreCities = new PartialSalonSearchViewModel();
                var dbResultMoreCities = DB.SalonDb.SearchSalonWithServicesMoreCities((long)UserCountry, city, gb, service, cat, date);
                searchResult.SalonModelMoreCities.Salons = SalonDbToViewModel(dbResultMoreCities, true);
                searchResult.SalonModelMoreCities.UrlWithPlaceHolder = $"/Salon/id/{service ?? 0}/{dtString}";
            }

            return View(searchResult);
        }


        [AllowAnonymous]
        [HttpPost]
        public ActionResult SearchResult(string search="",long? service = 0, long? city = 0,  long? cat = 0, double? lat = 0, double? lng = 0, DateTime? date = null,bool now=false)
        {
            GeoBound gb = null;
            if (lat.HasValue && lat >0  && (lng.HasValue && lng>0))
            {
                CoordinateBoundaries cb = null;
                try
                {
                    cb = new CoordinateBoundaries(lat.Value, lng.Value, 10, DistanceUnit.Kilometers);
                        gb = new GeoBound()
                        {
                            MaxLat = cb.MaxLat,
                            MinLat = cb.MinLat,

                            MinLng = cb.MinLng,
                            MaxLng = cb.MaxLng
                        };
                }
                catch (Exception ex)
                {
                    _log.Error("Error during searching process",ex);
                }
            }
            if (now) 
            {
                date=DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
            }
            var dbResult = DB.SalonDb.SearchSalonWithServices((long)UserCountry, city, gb, service, cat,date);
            var retModel = new PartialSalonSearchViewModel();
            retModel.Salons = SalonDbToViewModel(dbResult,true);
            string dtString = date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "";
            retModel.UrlWithPlaceHolder = $"/Salon/id/{service??0}/{dtString}";//{city??0}
            return PartialView("Salon/_SalonListWithServices",retModel);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult> Booking(long? service, long? salon, long? master, DateTime? date, TimeSpan? time, string clientComment)
        {
            var retResult = new BookingResultModel();

            if (!service.HasValue || !master.HasValue || !date.HasValue || !time.HasValue )
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            var serviceDuration = DB.ServiceCategoryDb.GetServiceDuration(master.Value, service.Value);
            if (serviceDuration <= 0)
            {
                retResult.Message = $"Chyba! Operáciu zopakujte neskôr";
                _log.Debug($"Service duration couldn't found for master {master.Value} and service {service.Value}");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            var timeFrom = time.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
            var to = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration));
            var isBooked = DB.BookingDb.IsAvailableMasterSlot(master.Value, date.Value, timeFrom, to);
            if (isBooked)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! tento termín nie je dostupný, zvoľte iný prosím";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            var bookConfirmNeed = DB.UserDb.IsConfirmNeedForBooking(master.Value);
            var bookConfirmed = bookConfirmNeed ? false : true;

            if (!Request.IsAuthenticated)
            {
                var sessionId = Guid.NewGuid();
                if (Session[StringConst.UserSessionName] == null)
                {
                    Session.Add(StringConst.UserSessionName, sessionId);
                }
                else
                {
                    sessionId = (Guid) Session[StringConst.UserSessionName];
                }
            
                var tempBook = new TempBookingDto()
                {
                    Created = DateTime.UtcNow,
                    MasterId = master,
                    Date = date,
                    From = time.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffset)),
                    To = timeFrom.Add(TimeSpan.FromMinutes( serviceDuration)),
                    SessionId = sessionId,
                    SalonId = salon,
                    ServiceId = service,
                    ClientComment = clientComment,
                    IsConfirmed = bookConfirmed
                };
                long bookSaveResult = 0;
                try
                {
                     bookSaveResult = DB.BookingDb.InsertUpdateTempBooking(tempBook);
                     _log.Info($"Temp booking created {salon} {service.Value}");
                }
                catch (Exception ex)
                {
                    _log.Error($"Error during saving temp record to Master with master id: {tempBook.MasterId} on service: {tempBook.ServiceId} session guid {sessionId}", ex);
                }

                if (bookSaveResult>0)
                {
                    retResult.IsSuccess = true;
                    retResult.Message = "Pre dokončenie rezervácie sa príhláste";
                    retResult.IsAuthNeed = true;

                    Session[StringConst.LastBookedConfirmNeed] = bookConfirmNeed;
                    TempData[StringConst.LastBookedConfirmNeed] = bookConfirmNeed;

                    if (Session[StringConst.NotFinishedBookingIds] == null)
                    {
                        Session.Add(StringConst.NotFinishedBookingIds,
                            new List<long>()
                            {
                                bookSaveResult
                            });
                    }
                    else
                    {
                        var bookingLst = (List<long>) Session[StringConst.NotFinishedBookingIds];
                        if (!bookingLst.Contains(bookSaveResult))
                        {
                            bookingLst.Add(bookSaveResult);
                            Session[StringConst.NotFinishedBookingIds] = bookingLst;
                        }
                    }
                }
            }
            else
            {
                try
                {
                    var booked = new BookedDto()
                    {
                        MasterId = master.Value,
                        SalonId = salon,
                        Added = DateTime.UtcNow,
                        ClientId = UserId,
                        Date = date.Value,
                        From = timeFrom,
                        To = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration)),
                        ServiceId = service.Value,
                        ClientComment = clientComment,
                        IsConfirmed = bookConfirmed
                    };
                    var bookId=DB.BookingDb.InsertBooking(booked);
                    DB.UserDb.BindUserToSalon(UserId,salon ?? 0, null);
                    _log.Info($"Successfully booked {UserId} {salon} {service.Value}");
                    retResult.IsSuccess = true;
                    var userInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(UserId);
                    var masterViber = DB.UserDb.GetUserViber(booked.MasterId);
                    var masterFacebook = DB.UserDb.GetUserFacebook(booked.MasterId);
                    var clientViber= DB.UserDb.GetUserViber(UserId);

                    if (!string.IsNullOrEmpty(masterFacebook)) // отправка сообщения на Messenger Facebook мастеру
                    {
                        var FbBot = new FacebookBotController();
                        string token = ConfigurationManager.AppSettings["FbPageToken"];
                        if (bookConfirmNeed)
                        {
                            string message = $"Máte novú NEPOTVRDENÚ rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber}). Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                            FbBot.SendNotify(token, masterFacebook, message);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 1, 0, 0, message, UserId, masterFacebook, null);
                        }
                        else
                        {
                            string message = $"Máte novú rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber}). Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                            FbBot.SendNotify(token, masterFacebook, message);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 1, 0, 0, message, UserId, masterFacebook, null);
                        }
                    }
                    else if (!string.IsNullOrEmpty(masterViber)) // шлём сообщение на Viber мастеру
                    {
                        //var res = MessageHelper.SendMsgWithButton($"Máte novú rezerváciu. {date.Value:dd.MM} {timeFrom:hh\\:mm}", masterViber, "Náhľad objednávok",
                        if (bookConfirmNeed)
                        {
                            // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!
                            string message = $"Máte novú NEPOTVRDENÚ rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber})";
                            var res = MessageHelper.SendMsgWithButton(message, masterViber, "Náhľad objednávok", ConfigurationManager.AppSettings["WebSiteLink"] + "Master/Calendar", 3);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 0, 1, 0, message, UserId, masterViber, null);
                        }
                        else
                        {
                            // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!
                            string message = $"Máte novú rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber})";
                            var res = MessageHelper.SendMsgWithButton(message, masterViber, "Náhľad objednávok", ConfigurationManager.AppSettings["WebSiteLink"] + "Master/Calendar", 3);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 0, 1, 0, message, UserId, masterViber, null);
                        }
                    }
                    else
                    {
                        //шлём СМС мастеру
                        /*var masterInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(booked.MasterId);
                        var m = new MessageSendingHelper();
                        if (bookConfirmNeed)
                        {
                            // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!
                            string message = $"Máte novú NEPOTVRDENÚ rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber}). Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                            var res = m.SendSms(masterInfo.PhoneNumber, message);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 0, 1, message, null);
                            _log.Debug($"Message about new reservation for master {booked.MasterId} successfully sent, sms info: {res}");
                        }
                        else
                        {
                            // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!
                            string message = $"Máte novú rezerváciu, {userInfo.Name} - {date.Value:dd.MM} o {time.Value:hh\\:mm} (t.č.: {userInfo.PhoneNumber}). Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                            var res = m.SendSms(masterInfo.PhoneNumber, message);
                            DB.MailingDb.AddUserSendMessage(booked.MasterId, 0, 1, message, null);
                            _log.Debug($"Message about new reservation for master {booked.MasterId} successfully sent, sms info: {res}");
                        }*/
                    }

                    // не нужно слать сообщение - показываем окно
                    /*if (!string.IsNullOrEmpty(userInfo.FacebookUserId)) // отправка сообщения на Messenger клиенту
                    {
                        // отправка сообщения клиенту на Messanger Facebook
                        var FbBot = new FacebookBotController();
                        string token = ConfigurationManager.AppSettings["FbPageToken"];
                        if (bookConfirmNeed)
                        {
                            string messengerResult = await FbBot.SendNotify(token, userInfo.FacebookUserId, "Vaša rezervácia bola prijatá, čakajte na schválenie. " + ConfigurationManager.AppSettings["WebSiteLink"] + "home/myschedule");
                            _log.Info($"Messenger resnose: {messengerResult}");
                        }
                        else
                        {
                            string messengerResult = await FbBot.SendNotify(token, userInfo.FacebookUserId, "Vaša rezervácia bola úspešná. " + ConfigurationManager.AppSettings["WebSiteLink"] + "home/myschedule");
                            _log.Info($"Messenger resnose: {messengerResult}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(clientViber))     // шлём сообщение на Viber клиенту
                    {
                        if (bookConfirmNeed)
                        {
                            var res = MessageHelper.SendMsgWithButton("Vaša rezervácia bola prijatá, čakajte na schválenie", clientViber, "Moje rezervácie", ConfigurationManager.AppSettings["WebSiteLink"] + "home/myschedule", 2);
                        }
                        else
                        {
                            var res = MessageHelper.SendMsgWithButton("Vaša rezervácia bola úspešná", clientViber, "Moje rezervácie", ConfigurationManager.AppSettings["WebSiteLink"] + "home/myschedule",2);
                        }
                    }*/

                    if (User.GetMasterCreatedStatus())
                    {
                        DB.UserDb.UpdateUserCreatedByStatus(User.GetUserId());
                        _log.Debug($"User was created by Master his status were changed");
                    }

                    //   TempData[StringConst.SuccessMessage] = "Dokončené!";
                    Session[StringConst.LastBookedId] = bookId;
                    TempData[StringConst.LastBookedId] = bookId;
                    Session[StringConst.LastBookedConfirmNeed] = bookConfirmNeed;
                    TempData[StringConst.LastBookedConfirmNeed] = bookConfirmNeed;
                    retResult.Url = "/Home/MySchedule";
                    retResult.IsAuthNeed = false;
                }
                catch (Exception ex)
                {
                    _log.Error("Error during save booking for Authorized customer",ex);
                }
            }
            return Json(retResult, JsonRequestBehavior.DenyGet);
        }


        /*[HttpPost]
        public ActionResult DisableCustomerViber()
        {
            var retResult = new ResultModel();
            try
            {
                var userId = UserId;
                DBManager.User.UserDbManager.Instance.DisableUserViber(userId);
            }
            catch (Exception ex)
            {
                _log.Error($"Error during disabling Viber Reminding for user {UserId}", ex);
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retResult, JsonRequestBehavior.DenyGet);
        }*/

        [HttpPost]
        public ActionResult Remind(long scheduleId, int bookBefore) // добавть напоминание за "bookBefore" до визита
        {
            var retResult = new ResultModel();
            try
            {
                var userId = UserId;
                var book = DB.BookingDb.GetBookedById(scheduleId);
                if (book != null)
                {
                    var bookConfirmNeed = DB.UserDb.IsConfirmNeedForBooking(book.MasterId);
                    if (!bookConfirmNeed)   // если не нужно подтверждение записей
                    {
                        var bookTime = book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString(@"hh\:mm");
                        string msg = $"Pripomíname Vám termín {book.Date:dd.MM} o {bookTime} v salóne {book.SalonName}. Váš odborník krasy {book.MasterName} sa na vás už teší";

                        TimeSpan timeRemindUtc;
                        if (bookBefore == 24)
                        {
                            timeRemindUtc = book.From;
                            book.Date = book.Date.AddDays(-1);
                        }
                        else
                        {
                            timeRemindUtc = book.From.Add(TimeSpan.FromHours(-bookBefore));
                        }
                        var dateUtc = new DateTime(book.Date.Year, book.Date.Month, book.Date.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);

                        DateTime datetime07 = new DateTime(book.Date.Year, book.Date.Month, book.Date.Day, 5, 0, 0);
                        if(DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно быть ночью (перенос на 20:00 предидущего дня)
                        {
                            dateUtc = dateUtc.AddDays(-1);
                            dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                        }

                        if (book.ClientId != null)
                        {
                            var userInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(book.ClientId.Value);
                            var bookId = DB.MailingDb.AddNewBookRemind(book.ClientId.Value, book.MasterId, scheduleId, msg, userInfo.ViberUserId, userInfo.FacebookUserId, userInfo.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);

                            _log.Info($"Reminder was added for book {bookId} user Id: {book.ClientId.Value}. Remind at (time) {book.From}");
                            retResult.IsSuccess = true;
                            retResult.Message = "Dokončené!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during saving Remind for user {UserId} Schedule {scheduleId} remindBefore {bookBefore}", ex);
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
            }

            return Json(retResult, JsonRequestBehavior.DenyGet);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult FooterMenu()
        {
            var lst = base.GetFooterLinks();
            return PartialView("_Footer", lst);
        }

        [HttpPost]
        public ActionResult MoveCustomerBooking(long scheduleId,long service,long masterId,long salonId, DateTime? date, TimeSpan? time)
        {
            var retResult = new BookingResultModel();
            if (!date.HasValue || !time.HasValue)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            var serviceDuration = DB.ServiceCategoryDb.GetServiceDuration(masterId, service);
            if (serviceDuration <= 0)
            {
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                retResult.IsSuccess = false;
                _log.Debug($"Coundn't get service duration during change date of booking by client. Client {User.GetUserId()} booking id{scheduleId}");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            var timeFrom = time.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
            var to = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration));
            var isBooked = DB.BookingDb.IsAvailableMasterSlot(masterId, date.Value, timeFrom, to);
            if (isBooked)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! tento termín nieje dostupný, zvoľte iný prosím.";
                _log.Debug($"Change date by customer not success - master is busy. clientId {UserId}  ");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            try
            {
                //var book = DB.BookingDb.GetBooked(scheduleId, masterId);
                var book = DB.BookingDb.GetBookedById(scheduleId);
                book.From = book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));

                if (DB.BookingDb.UpdateBookingByClient(scheduleId, UserId, date.Value, timeFrom, to))
                {
                    _log.Info($"Client {UserId} succsessfully moved booking {scheduleId} on new Date: {date.Value:dd.MM} and time: {time.Value:hh\\:mm}");
                    
                    var userInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(book.ClientId.Value);
                    if (book.IsConfirmed) // если запись подтверждена
                    {
                        DB.BookingDb.RemoveBookRemind(scheduleId, UserId); // удалить текущее напоминание
                        _log.Info($"Reminder for book: {scheduleId} user Id: {UserId} was deleted");
                        // здесь можно добавить уведомление мастеру, что запись перенесена и от него требуется подтверждение
                        string msg = $"Pripomíname Vám termín {date.Value:dd.MM} o {time.Value:hh\\:mm} v salóne {book.SalonName}. Váš odborník krasy {book.MasterName} sa na vás už teší";
                        TimeSpan timeRemindUtc = timeFrom.Add(TimeSpan.FromHours(-6));
                        DateTime dateUtc;
                        if (timeRemindUtc.TotalMinutes > 0)
                        {
                            dateUtc = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);
                        }
                        else
                        {
                            dateUtc = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0);
                        }

                        DateTime datetime07 = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 5, 0, 0);
                        if (DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно быть ночью (перенос на 20:00 предидущего дня)
                        {
                            dateUtc = dateUtc.AddDays(-1);
                            dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                        }

                        var bookId = DB.MailingDb.AddNewBookRemind(book.ClientId.Value, book.MasterId, scheduleId, msg, userInfo.ViberUserId, userInfo.FacebookUserId, userInfo.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);
                        _log.Info($"Reminder was added for book {scheduleId} user Id: {book.ClientId.Value}. Remind at time: {timeRemindUtc.Add(TimeSpan.FromHours(2)):hh\\:mm}");
                    }

                    retResult.IsSuccess = true;
                    retResult.Message = "Dokončené!";
                    try
                    {
                        var masterViber = DB.UserDb.GetUserViber(masterId);
                        var masterFacebook = DB.UserDb.GetUserFacebook(masterId);
                        string strBookConfirmed = book.IsConfirmed ? "" : "nepotvrdenú";

                        if (!string.IsNullOrEmpty(masterFacebook)) // отправка сообщения на Messenger Facebook мастеру
                        {
                            var FbBot = new FacebookBotController();
                            string token = ConfigurationManager.AppSettings["FbPageToken"];
                            string message = $"Zákazník {User.GetFullName()} (t.č.: {userInfo.PhoneNumber}) preniesol {strBookConfirmed} rezerváciu z {book.Date:dd.MM} o {book.From:hh\\:mm} na termín {date.Value:dd.MM} o {time.Value:hh\\:mm}. Náhľad objednávok: www.beautyclick.sk/Master/Calendar";
                            FbBot.SendNotify(token, masterFacebook, message);
                            DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message, UserId, masterFacebook, null);
                        }
                        else if (!string.IsNullOrEmpty(masterViber)) // шлём сообщение на Viber мастеру
                        {
                            string message = $"Zákazník {User.GetFullName()} (t.č.: {userInfo.PhoneNumber}) preniesol {strBookConfirmed} rezerváciu z {book.Date:dd.MM} o {book.From:hh\\:mm} na termín {date.Value:dd.MM} o {time.Value:hh\\:mm}";
                            var res = MessageHelper.SendMsgWithButton(message, masterViber, "Náhľad objednávok", ConfigurationManager.AppSettings["WebSiteLink"] + "Master/Calendar", 3);
                            DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message, UserId, masterViber, null);
                        }
                        else
                        {
                            // шлём СМС мастеру о переносе записи клиентом
                            /*var masterInfo = DB.UserDb.GetUserShortInfoForMasterByUserId(masterId);
                            var m = new MessageSendingHelper();
                            // НУЖНО сделать, чтобы по ссылке кидало на конкретный день календаря !!!
                            string message = $"Zákazník {User.GetFullName()} (t.č.: {userInfo.PhoneNumber}) preniesol {strBookConfirmed} rezerváciu z {book.Date:dd.MM} o {book.From:hh\\:mm} na {date.Value:dd.MM} o {time.Value:hh\\:mm} www.beautyclick.sk/Master/Calendar";
                            var res = m.SendSms(masterInfo.PhoneNumber, message);
                            DB.MailingDb.AddUserSendMessage(masterId, 0, 1, message, null);
                            _log.Debug($"Message about change reservation by Client {User.GetFullName()} (id: {User.GetUserId()}) successfully sent, sms info: {res}");*/
                        }

                        var isMessagesDisabled = DB.UserDb.IsMessagesDisabled(UserId); // если пользователь отказался получать уведомления
                        if (!isMessagesDisabled)
                        {
                            if (!string.IsNullOrEmpty(userInfo.FacebookUserId)) // Сообщение на Messenger клиенту
                            {
                                var FbBot = new FacebookBotController();
                                string token = ConfigurationManager.AppSettings["FbPageToken"];
                                string message = $"Vaša rezervácia bola úspešne prenesená z {book.Date:dd.MM} o {book.From:hh\\:mm} na termín {date.Value:dd.MM} o {time.Value:hh\\:mm}. www.beautyclick.sk/Home/MySchedule";
                                FbBot.SendNotify(token, userInfo.FacebookUserId, message);
                                DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message, UserId, userInfo.FacebookUserId, null);
                            }
                            else if (!string.IsNullOrEmpty(userInfo.ViberUserId)) // Сообщение на Viber клиенту
                            {
                                string message = $"Vaša rezervácia bola úspešne prenesená z {book.Date:dd.MM} o {book.From:hh\\:mm} na termín {date.Value:dd.MM} o {time.Value:hh\\:mm}";
                                var res = MessageHelper.SendMsgWithButton(message, userInfo.ViberUserId, "Moje rezervácie", ConfigurationManager.AppSettings["WebSiteLink"] + "Home/MySchedule/", 3);
                                DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message, UserId, userInfo.ViberUserId, null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error during sending notifications about moved schedule. ClientId: {UserId}, bookid: {scheduleId}",ex);
                    }
                }
                else
                {
                    _log.Debug($"Error during moving booking by client. ClientId: {UserId}, booking id: {scheduleId}");
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr!";
                }
                retResult.IsAuthNeed = false;
            }
            catch (Exception ex)
            {
                _log.Error($"Error during change date of booking for customer {UserId}", ex);
            }
            return Json(retResult, JsonRequestBehavior.DenyGet);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult QL(string hash)
        {
            _log.Debug("Open by quick link hash: " + hash);
            try
            {
                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                    Session.RemoveAll();
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during logoff user, with quick link. ",ex);
            }

            var userId = DB.UserDb.UserIdByQuickLinkHash(hash);
            if (userId > 0)
            {
                var user = UserManager.FindById(userId);
                SignInManager.SignIn(user, true, true);

                if (user == null)
                {
                    _log.Error($"Error during login by hash {userId}  {hash}");
                    return RedirectToAction("Index");
                }

                _log.Info($"Customer successfully authorized by link {hash} with UserId  {userId}  ");
                //return RedirectToAction("MySchedule");
                return RedirectToAction("MySchedule", new { id = 1 });
            }

            _log.Error($"Bad trying to login via QL by hash {hash}");
            return Redirect("Index");
        }

        public ActionResult SalonMaster(long? master, long? service, DateTime? date)
        {
            if (master == null)
            {
                return RedirectToAction("Index");
            }
            //var Master
            return View("Master");
        }
        
        [Authorize]
        public ActionResult Favorite()
        {
            var dbResult = DB.SalonDb.GetFullSalonInfoByFavList(User.GetFavoriteSalons());
            var model = new PartialSalonSearchViewModel();
            model.Salons = SalonDbToViewModel(dbResult, true);
            model.UrlWithPlaceHolder = "/Salon/id";
            model.IsShowMasterService = false;
            model.Salons.Shuffle();
            return View(model);
        }

        [Authorize]
        public ActionResult GetMyFavorite()
        {
            var dbResult = DB.SalonDb.GetFullSalonInfoByFavList(User.GetFavoriteSalons());
            var model = new PartialSalonSearchViewModel();
            model.Salons = SalonDbToViewModel(dbResult, true);
            model.UrlWithPlaceHolder = "/Salon/id";
            model.IsShowMasterService = false;
            model.Salons.Shuffle();
            ViewBag.DisplayType = Shared.Enums.SalonListViewType.Favorite;
            return PartialView("Salon/_SalonListWithServices", model);
        }

        [Authorize]
        [HttpPost]
        public ActionResult UpdateFavoriteSalon(long salon, bool status,long? master)
        {
            var retResult = new ResultModel();
            try
            {
                var res = DB.UserDb.UpdateFavoriteSalon(salon, UserId, status, master);
                if (res > 0)
                {
                    _log.Info($"User {UserId} successfully update Favorite status for salon {salon} new status {status}");
                    retResult.IsSuccess=true;
                    retResult.Message = "Dokončené!";
                }
                else
                {
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during update favorite status for User {UserId} Salon {salon} new status {status} ",ex);
            }
            ViewBag.DisplayType = Shared.Enums.SalonListViewType.Favorite;

            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult FillQuizBeautyClick(string salonName, string timeForBook, string canClientForget, string didYouTryOnlineReservationSystem,
            string didYouTryOnlineReservationSystemText, string howMuchNewClientsNeed, string ourAdsMoreEffective, string youKnowAboutRating, string howLongWeWillUsePaper)
        {
            var retResult = new ResultModel();
            try
            {
                SmtpClient objSmtp = new SmtpClient();

                objSmtp.EnableSsl = true;
                objSmtp.Host = ConfigurationManager.AppSettings["SmtpServer"];
                objSmtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["SMTPPort"]);
                objSmtp.UseDefaultCredentials = false;
                objSmtp.Credentials = new System.Net.NetworkCredential(ConfigurationManager.AppSettings["SmtpUserName"], ConfigurationManager.AppSettings["SmtpPassword"]);
                objSmtp.DeliveryMethod = SmtpDeliveryMethod.Network;

                string messSubject = "Výsledok dotazníku";
                string messBody = "<p>Názov salóna a kontaktný telefón prevádzky<br />" + salonName + "</p>";

                messBody += "<p>Koľko času vám zaberie objednávanie zákazníkov?<br />";
                switch (timeForBook)
                {
                    case "5min": messBody += "5 min. denne"; break;
                    case "30min": messBody += "30 min. denne"; break;
                    case "more40min": messBody += "viac ako 40 min. denne"; break;
                }
                messBody += "</p>";

                messBody += "<p>Stáva sa vám že zákazník na termín zabudne?<br />";
                switch (canClientForget)
                {
                    case "yes": messBody += "áno"; break;
                    case "no": messBody += "nie"; break;
                }
                messBody += "</p>";

                messBody += "<p>Vyskúšali ste niekedy on-line rezervačný systém objednávania zákazníkov?<br />";
                switch (didYouTryOnlineReservationSystem)
                {
                    case "yes": messBody += "áno. Aký: " + didYouTryOnlineReservationSystemText; break;
                    case "no": messBody += "nie. Prečo: " + didYouTryOnlineReservationSystemText; break;
                }
                messBody += "</p>";

                messBody += "<p>Koľko nových zákazníkov potrebuje vaša prevádzka aby ste mali plný diár?<br />";
                switch (howMuchNewClientsNeed)
                {
                    case "0": messBody += "Nepotrebujem nových zákazníkov"; break;
                    case "5-10": messBody += "Od 5-10 mesačne"; break;
                    case "10": messBody += "Viac ako 10 mesačne"; break;
                }
                messBody += "</p>";

                messBody += "<p>Vedeli ste, že príspevky, ktoré uverejňujete na FB alebo IG vidí iba 30% vašich sledovateľov? Ponuka voľných termínov týmto spôsobom nieje tak efektívna ako on-line rezervačný portál BeautyClick.sk<br />";
                switch (ourAdsMoreEffective)
                {
                    case "yes": messBody += "áno, viem o tom"; break;
                    case "no": messBody += "nie, neviem o tom"; break;
                }
                messBody += "</p>";

                messBody += "<p>Hodnotenia zákazníkov sú dôležité pri výbere zákazníka, ktorý hľadá kaderníčku, manikérku, masáž... Chceli by ste, aby vám každý zákazník napísal recenziu a zvýšil Vám váš rating?<br />";
                switch (youKnowAboutRating)
                {
                    case "yes": messBody += "áno, recenzie sú dôležité"; break;
                    case "no": messBody += "nie, nepotrebujem recenzie"; break;
                }
                messBody += "</p>";

                messBody += "<p>Dnes už používame miesto listov messenger, platíme bezkontaktne, máme google namiesto knihy zlaté stránky, nakupujeme on-line a používame aplikácie. Čo myslíte ako dlho ešte budeme používať papierové zápisníky, aby sme zapísali zákazníka na termín?<br />";
                switch (howLongWeWillUsePaper)
                {
                    case "now": messBody += "Už je čas ísť s dobou"; break;
                    case "1year": messBody += "Ešte 1 rok"; break;
                    case "more1year": messBody += "Viac ako 1 rok"; break;
                }
                messBody += "</p>";

                var senderEmail = new MailAddress(ConfigurationManager.AppSettings["FromEmail"], "BeautyClick");
                var receiverEmail = new MailAddress(ConfigurationManager.AppSettings["EmailForQuizResults"], "Michaela");
                using (var mess = new MailMessage(senderEmail,receiverEmail)
                {
                    IsBodyHtml = true,
                    Subject = messSubject,
                    Body = messBody,
                    Priority = MailPriority.Normal
                })
                {
                    objSmtp.Send(mess);   // send an email with Quiz Results
                }
                retResult.IsSuccess = true;
                retResult.Message = "Ďakujem za spoluprácu";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }
            catch (Exception)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Vyskytol nejaký problém, skúste ešte raz!";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }
            //return await Task.Run(() => Json(retResult, JsonRequestBehavior.AllowGet));
            //return Json(retResult, JsonRequestBehavior.AllowGet);
        }


        [Authorize]
        [HttpPost]
        public ActionResult AddFeedBack(long book,long salon, long master, short mark, string text, bool anon)
        {
            var retResult = new ResultModel();
            try
            {
                var isBookExist = DB.BookingDb.GetBookedForCustomer(book, UserId);
                if (isBookExist == null)
                {
                    _log.Debug($"trying to add feedback which not booked by user.  slot id {book} userId:{UserId}");
                }
                var res = DB.SalonDb.AddSalonFeedBack(salon, master, UserId, mark, text, anon, book);
                if (res > 0)
                {
                    _log.Info($"User {UserId} successfully added new feedback for salon {salon} , book {book}, feedbackid {res}");
                    retResult.IsSuccess = true;
                    retResult.Message = "Dokončené!";
                }
                else
                {
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                }
            }
            catch (Exception ex)
            {

                _log.Error($"Error during adding new feedback userid: {UserId} for salon :{salon} book {book}",ex);
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        private List<SalonSearchModel> SalonDbToViewModel(List<SalonSearchWithServicesDto> dbModel, bool shuffleServices)
        {
            var retVal = new List<SalonSearchModel>();
            if (dbModel != null)
            {
                var salons = dbModel.Select(a => new SalonSearchModel()
                {
                    Name = a.Name,
                    Id = a.SalonId,
                    MainPhoto = a.MainPhoto,
                    Address = a.Address,
                    City = a.City,
                    CityArea = a.CityArea,
                    Rating = a.rating,
                    RatingCount = a.ratingcount,
                    BookesCount = a.bookescount
                }).DistinctBy(a => a.Id).ToList();
                var favSalons = User.GetFavoriteSalons();
                foreach (var salon in salons)
                {
                    salon.Services = new List<Site.Models.Search.SalonServiceModel>();
                    salon.IsFavorite = favSalons.Contains(salon.Id);
                    salon.Services = dbModel.Where(a => a.SalonId == salon.Id).Select(s => new Site.Models.Search.SalonServiceModel()
                        {
                            Price = s.Price,
                            ServiceId = s.ServiceId,
                            SalonId = salon.Id,
                            ServiceName = s.ServiceName,
                            ServiceDescription = s.ServiceDescription,
                            Duration = s.Duration,
                            ServiceNameAlt = s.ServiceNameAlt,
                            MasterId = s.MasterId
                        }).ToList();
                    salon.Services = salon.Services.OrderBy(a => a.ServiceId).ThenBy(a => a.Price).DistinctBy(a => a.ServiceId).ToList().Take(1).ToList();
                    if (shuffleServices)
                    {
                        salon.Services.Shuffle();
                    }
                    retVal.Add(salon);
                }
            }
            return retVal;
        }
    }
}

#region comm 
/*
     [AllowAnonymous]
        public ActionResult S(long id)
        {
            var retVal = new List<Site.Models.Salon.SalonViewModel>();
            return View("Salons",retVal);
        }

        [AllowAnonymous]
        //salon-by-date
        public ActionResult D(string id)
        {
            var retVal = new List<Site.Models.Salon.SalonViewModel>();
            return View("Salons", retVal);
        }

        [AllowAnonymous]
        //salon-by-date and service
        public ActionResult V(long id,string secPar)
        {
            var retVal = new Site.Models.Salon.SalonsViewModel();

            var lst = DB.SalonDb.SearchSalon(1, null, 25, 08, 2020);
            
            var salons = lst.Select(a => new SalonDto()
            {
                Name = a.Name,
                Address = a.Address,
                CategoryId = a.CategoryId,
                CountryId = a.CountryId,
                CityId = a.CityId??0,
                Description = a.Description,
                GoogleLat = a.GoogleLat,
                GoogleLng = a.GoogleLng,
                Id = a.SalonId,
                MainPhoto = a.MainPhoto
            }).DistinctBy(a => a.Id).ToList();
            var services = lst.Select(a => new ServiceDto
            {
                DefaultDuration = a.ServiceDuration,
                DefaultPrice = a.ServicePrice ?? 0,
                Name = a.ServiceName,
                NameAlt = a.ServiceNameAlt,
                Id = a.ServiceId ?? 0,
                SalonId = a.SalonId
            }).Where(a => a.Id > 0).GroupBy(a=>a.SalonId).ToList();
            var masterSchedule = lst.Select(m => new MasterScheduleDto()
            {
                MasterId = m.MsMasterId ?? 0,
                Month = m.MsMonth ?? 0,
                Date = m.MsDate ?? DateTime.MinValue,
                Day = m.MsDay ?? 0,
                Year = m.MsYear ?? 0,
                From = m.MsFrom ?? new TimeSpan(0, 0, 0),
                To = m.MsTo ?? new TimeSpan(0, 0, 0),
            }).Where(a => a.MasterId > 0).DistinctBy(a=>a.MasterId).GroupBy(a => a.MasterId).ToList(); 

            var booked = lst.Select(b => new BookedDto()
            {
                MasterId = b.BookedMasterId ?? 0,
                From = b.BookedFrom ?? new TimeSpan(0, 0, 0),
                Date = b.BookedDate ?? DateTime.MinValue,
                To = b.BookedTo ?? new TimeSpan(0, 0, 0)
            }).Where(a => a.MasterId > 0).GroupBy(a => a.MasterId).ToList();

            retVal.Salons = new List<SalonViewModel>();

            foreach(var s in salons)
            {
                retVal.Salons.Add(new SalonViewModel());
                
            }
            return View("Salons", lst);
        }
 */
#endregion