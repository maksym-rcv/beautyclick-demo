using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DBManager.Subscription;
using Dto.Entities;
using Dto.Entities.Custom;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Newtonsoft.Json;
using Shared.Enums;
using Site.DB;
using Site.Extentions;
using Site.Helper;
using Site.Models;
using Site.Models.Master;
using Site.Models.Salon;
using ViberSharedLogic;
using MasterScheduleModel = Site.Models.Master.MasterScheduleModel;
using Role = Site.Helper.Role;

namespace Site.Controllers
{

    //[CustomAuthorization(ActionValue = "Login", AreaValue = "Video", ControllerValue = "controller2Name")]
    [AuthorizeRoles(Site.Helper.Role.SalonAdmin, Site.Helper.Role.SuperAdmin, Site.Helper.Role.Master)]

    public class MasterController : BaseController
    {
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(MasterController));
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public MasterController()
        {
            //var sendREs = SlovakSmsProvider.SendSms("+4210944370349", "Master created booking for you. Open link to get details https://gooogle.com");
        }
        public MasterController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }


        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>(); }
            private set { _signInManager = value; }
        }

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>(); }
            private set { _userManager = value; }
        }

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        // GET: Master

        [AuthorizeRoles(Site.Helper.Role.Master)]
        public ActionResult Index(int? month, int? day)
        {
            return RedirectToAction("Calendar");
        }

        public ActionResult Schedule(long id, int year, int month, int day) // добавлен параметр year
        {
            if (month < 1 || month > 12 || day <= 0 || day >= 32)
            {
                return View("StopCheating");
            }

            var retVal = new MasterScheduleModel()
            {
                Schedule = DB.MasterDb.GetMasterScheduleAndBooked(id, OwnerId(), year, month, day),

                DayNavigation = new DayNavigation()
                {
                    Link = "/Master/ScheduleLoad/" + id + "/",
                    Now = new DateTime(DateTime.UtcNow.Year, month, day),
                    Lang = (Language) LangId
                }
            };
            return View(retVal);
        }

        [HttpPost]
        public ActionResult MasterScheduleDay(long id, int month, int day)
        {
            if (month < 1 || month > 12 || day <= 0 || day >= 32)
            {
                return View("StopCheating");
            }

            var retVal = new MasterScheduleWithMasterInfoModel(base.ClientBrowserUtcOffset)
            {
                Schedule = DB.MasterDb.GetMasterScheduleAndBooked(id, OwnerId(), 2022, month, day), // добавил 2022 год вручную!!!
            };
            return PartialView("_MasterSchedule", retVal);
        }

        public ActionResult GetBookCalendar(int month, int year, bool isToday = false, long master = 0)
        {
            //Todo:validate loading for masterId sec
            var model = BookModelHelper.GenerateModel(month, year,true, isToday, master);
            return PartialView("_MasterCalendar", model);
        }
        public ActionResult GetCancelDatesCalendar(int month, int year, bool isToday = false, long master = 0)
        {
            //Todo:validate loading for masterId sec
            var model = BookModelHelper.GenerateModelForCancelDates(month, year, true, isToday, master);
            return PartialView("_MasterCancelDatesCalendar", model);
        }

        public ActionResult GetWorkingTime(int day, int month, int year, long masterId = 0)
        {
            var model = new MasterCalendarModel();
            var curDate = new DateTime(year, month, day);

            model.WorkingTimeFrom = DB.MasterDb.GetWorkingTimeFrom(masterId, curDate);
            model.WorkingTimeTo = DB.MasterDb.GetWorkingTimeTo(masterId, curDate);
            if (model.WorkingTimeFrom == TimeSpan.Parse("23:59:00"))
                model.WorkingTimeFrom = TimeSpan.Parse("00:00:00");
            else
                model.WorkingTimeFrom = model.WorkingTimeFrom.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
            if (model.WorkingTimeTo != TimeSpan.Parse("00:00:00"))
                model.WorkingTimeTo = model.WorkingTimeTo.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));

            return PartialView("_MasterWorkingTime", model);
        }

        public string GetWorkingTimeFrom(int day, int month, int year, long masterId = 0)
        {
            var curDate = new DateTime(year, month, day);

            TimeSpan workingTimeFrom = DB.MasterDb.GetWorkingTimeFrom(masterId, curDate);
            if (workingTimeFrom == TimeSpan.Parse("23:59:00"))
                workingTimeFrom = TimeSpan.Parse("00:00:00");
            else
                workingTimeFrom = workingTimeFrom.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));

            return workingTimeFrom.ToString("hh\\:mm");
        }
        public string GetWorkingTimeTo(int day, int month, int year, long masterId = 0)
        {
            var curDate = new DateTime(year, month, day);

            TimeSpan workingTimeTo = DB.MasterDb.GetWorkingTimeTo(masterId, curDate);
            if (workingTimeTo != TimeSpan.Parse("00:00:00"))
                workingTimeTo = workingTimeTo.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));

            return workingTimeTo.ToString("hh\\:mm");
        }


        [AuthorizeRoles(Site.Helper.Role.SalonAdmin, Site.Helper.Role.Master)]
        [HttpGet]
        public ActionResult Clients(long? id)   // список клиентов
        {
            if (id == null)
            {
                id = MasterId();
            }
            if (id == 0)
            {
                id = MasterId();
            }
            var retModel = Site.Helper.ModelHelper.MasterHelper.GenerateMasterClientsModel(id.Value);

            return View(retModel);
        }

        public ActionResult Calendar(long? id)
        {
            if (id == null)
            {
                id = MasterId();
            }
            if (id == 0)
            {
                id = MasterId();
            }

            //var masterInfoWithService = DB.MasterDb.GetMasterInfoWithService(id.Value, UserId);
            var masterInfoWithService = DB.MasterDb.GetMasterInfoWithService(id.Value, DB.MasterDb.GetOwnerByMasterId(id.Value));
            if(masterInfoWithService == null)
            {
                return RedirectToAction("MasterServices", "SalonOwner", new { id });
            }
            var masterInfo = masterInfoWithService.FirstOrDefault();
            if (masterInfoWithService.Count <= 0 || masterInfo == null)
            {
                return RedirectToAction("MasterServices", "SalonOwner", new { id });
            }

            var retModel = new MasterCalendarModel();
            retModel.MasterId = id.Value;
            retModel.MasterName = masterInfo.Name;
            retModel.Avatar = masterInfo.Avatar;
            long salonId = 0;
            retModel.MasterServices = new List<ServiceDto>();
            foreach (var services in masterInfoWithService)
            {
                if(services.ServiceId != null)
                {
                    if (DB.MasterDb.GetServiceSpecialityById((long)services.ServiceId) != 61)
                    {
                        retModel.MasterServices.Add(new ServiceDto()
                        {
                            DefaultDuration = services.Duration,
                            DefaultPrice = services.Price ?? 0,
                            Name = services.ServiceName,
                            Description = services.ServiceDescription,
                            SalonId = services.SalonId,
                            Id = services.ServiceId ?? 0
                        });
                        if (salonId == 0 && services.SalonId.HasValue)
                        {
                            salonId = services.SalonId.Value;
                        }
                    }
                }
            }
            retModel.SalonId = masterInfo.SalonId??0;
            var today = DateTime.UtcNow.AddHours(base.ClientBrowserUtcOffset*-1);

            retModel.WorkingTimeFrom = DB.MasterDb.GetWorkingTimeFrom(id.Value, today);
            retModel.WorkingTimeTo = DB.MasterDb.GetWorkingTimeTo(id.Value, today);
            if (retModel.WorkingTimeFrom == TimeSpan.Parse("23:59:00"))
                retModel.WorkingTimeFrom = TimeSpan.Parse("00:00:00");
            else
                retModel.WorkingTimeFrom = retModel.WorkingTimeFrom.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
            if (retModel.WorkingTimeTo != TimeSpan.Parse("00:00:00"))
                retModel.WorkingTimeTo = retModel.WorkingTimeTo.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));

            retModel.BookSchedule = BookModelHelper.GenerateModel(today.Month, today.Year, true, true, id.Value);


            retModel.MasterScheduleModel = new MasterScheduleWithMasterInfoModel(base.ClientBrowserUtcOffset)
            {
                //Schedule = DB.MasterDb.GetMasterScheduleAndBooked(id.Value, OwnerId(), today.Month, today.Day)
                Schedule = DB.MasterDb.GetMasterScheduleAndBooked(id.Value, DB.MasterDb.GetOwnerByMasterId(id.Value), today.Year, today.Month, today.Day)
            };
            if (retModel.MasterScheduleModel.Schedule != null)
            {
                //whole day busy.
                if (retModel.MasterScheduleModel.Schedule.Count == 1)
                {
                    var s = retModel.MasterScheduleModel.Schedule.FirstOrDefault();
                    if (s?.BookedFrom != null && s.BookedFrom.Value == new TimeSpan(0, 0, 0) && s.BookedTo.HasValue && s.BookedTo.Value == new TimeSpan(23, 59, 0))
                    {
                        retModel.MasterScheduleModel.Schedule.Add(new MasterScheduleAndBookedDto()
                        {
                            ServiceId = null,
                            Month = (short) today.Month,
                            Day = (short) today.Day,
                            Year = today.Year,
                            BookedFrom = retModel.MasterScheduleModel.From,
                            BookedTo = retModel.MasterScheduleModel.To,
                        });
                    }
                }
                else
                {
                    foreach (var s in retModel.MasterScheduleModel.Schedule)
                    {
                        if (s.BookedFrom.HasValue && s.BookedTo.HasValue)
                        {
                            s.BookedFrom = s.BookedFrom.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                            s.BookedTo = s.BookedTo.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                        }
                    }
                }
            }
            retModel.DayToEndOfSubscription = SubscriptionDb.GetDaysBeforeEndOfSubscription(salonId);
            retModel.NotConfirmedBookedCount = DB.MasterDb.GetMasterNotConfirmedBookedCount(id.Value);
            return View(retModel);
        }


        [HttpPost]
        //public ActionResult AddScheduleNewReservation(MasterAddScheduleJson json)
        public async System.Threading.Tasks.Task<ActionResult> AddScheduleNewReservation(MasterAddScheduleJson json)
        {
            if (json == null)
            {
                _log.Error("error Master Add Schedule json is null");
                return Json(new ResultModel() { IsSuccess = false });
            }
            json.salonId = DB.SalonDb.GetSalonIdByMasterId(json.masterId);

            if (json.clientPhone == null)
                json.clientPhone = "";

            var retResult = new ResultModel();

            CultureInfo provider = CultureInfo.InvariantCulture;
            var date = DateTime.ParseExact(json.date, StringHelper.DateTimeParse, provider, DateTimeStyles.None);
            var now = DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
            if (date.Date < now.Date)
            {
                _log.Debug($"Master {json.masterId} tried to book for past date {date}  {json}");
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }
            var serviceDuration = DB.ServiceCategoryDb.GetServiceDuration(json.masterId, json.serviceId);
            if (serviceDuration <= 0)
            {
                retResult.Message = $"Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            var timeFrom = json.time.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
            var to = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration));
            var isBooked = DB.BookingDb.IsAvailableMasterSlot(json.masterId, date, timeFrom, to);
            if (isBooked)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! tento termín nieje dostupný, zvoľte iný prosím.";
                _log.Debug($"Booking time is occupated userId: {UserId} date {json.date}  time from utc {json.time}, time from {timeFrom}, time to{to}");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            UserSmallInfoForMaster user = null;

            if (json.customerId > 0 && json.clientPhone == "")
                json.clientPhone = DB.UserDb.GetUserPhoneById(json.customerId.Value);

            user = GetOrCreateUser(json.clientPhone, json.clientName, json.masterId, json.customerId);
            if (user == null)
            {
                _log.Error($"Can't create/find user for booking, model: {json}. MasterId: {json.masterId}");
                retResult.IsSuccess = true;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }

            var booked = new BookedDto()
            {
                MasterId = json.masterId,
                SalonId = json.salonId,
                Added = DateTime.UtcNow,
                ClientId = user.Id,
                Date = date,
                From = timeFrom,
                To = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration)),
                ServiceId = json.serviceId,
                MasterComment = json.masterComment,
                IsConfirmed = true
            };
            try
            {
                retResult.Id = DB.BookingDb.InsertBooking(booked);
                if (retResult.Id > 0)
                {
                    DB.UserDb.BindUserToSalon(user.Id, json.salonId, json.clientName);
                    _log.Debug($"Booking successfully created by master: {json.masterId} for service {json.serviceId} for user {user.Id} date {booked.Date:dd.MM.yyyy}, time from: {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}, time to: {booked.To.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}");

                    string salonName = "";
                    var salonDto = DB.SalonDb.GetSalonInfo(json.salonId);
                    var first = salonDto.FirstOrDefault();
                    if (first != null)
                    {
                        salonName = first.Name;
                    }
                    var masterName = DB.MasterDb.GetMasterShortInfo(json.masterId, DB.MasterDb.GetOwnerByMasterId(json.masterId), LangId).Name;
                    if (masterName == null)
                        masterName = "";

                    if(json.clientPhone[0] == '0')
                    {
                        // добавить напоминание клиенту о записи за 3 часа до начала
                        string msg = $"Pripomíname Vám termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm} v salóne {salonName}. Váš odborník krasy {masterName} sa na vás už teší";
                        TimeSpan timeRemindUtc = timeFrom.Add(TimeSpan.FromHours(-6));
                        DateTime dateUtc;
                        if (timeRemindUtc.TotalMinutes > 0)
                        {
                            dateUtc = new DateTime(date.Year, date.Month, date.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);
                        }
                        else
                        {
                            dateUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                        }

                        DateTime datetime07 = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 5, 0, 0);
                        if (DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно быть ночью (перенос на 20:00 предидущего дня)
                        {
                            dateUtc = dateUtc.AddDays(-1);
                            dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                        }

                        var bookId = DB.MailingDb.AddNewBookRemind(user.Id, json.masterId, retResult.Id, msg, user.ViberUserId, user.FacebookUserId, user.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);
                        _log.Info($"Reminder was added for book {retResult.Id} user Id: {user.Id}. Remind at time: {timeRemindUtc.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}");

                        try
                        {
                            var isMessagesDisabled = DB.UserDb.IsMessagesDisabled(user.Id); // если пользователь отказался получать уведомления
                            var hashLink = StringHelper.GenerateString(8);
                            // Добавить проверку на дублирование с существующими записями в БД !
                            var q = new QuickLoginLinkDto()
                            {
                                UserId = user.Id,
                                Added = DateTime.UtcNow,
                                Hash = hashLink
                            };
                            DB.UserDb.AddUserQuickLink(q);
                            _log.Debug("Quick link successfully saved");

                            if (!string.IsNullOrEmpty(user.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                            {
                                if (!isMessagesDisabled)
                                {
                                    var FbBot = new FacebookBotController();
                                    string token = ConfigurationManager.AppSettings["FbPageToken"];
                                    string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu: www.beautyclick.sk/{q.Hash}";
                                    await FbBot.SendNotify(token, user.FacebookUserId, message);
                                    DB.MailingDb.AddUserSendMessage(json.masterId, 1, 0, 0, message, user.Id, user.FacebookUserId, null);
                                }
                            }
                            else if (!string.IsNullOrEmpty(user.ViberUserId))   // отправить сообщение на Viber клиенту
                            {
                                if (!isMessagesDisabled)
                                {
                                    string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu:";
                                    var res = MessageHelper.SendMsgWithButton(message, user.ViberUserId, "Moje rezervácie", $"https://beautyclick.sk/{q.Hash}", 3);
                                    DB.MailingDb.AddUserSendMessage(json.masterId, 0, 1, 0, message, user.Id, user.ViberUserId, null);  // записать сообщение в базу
                                }
                            }
                            else
                            {
                                if (ConfigurationManager.AppSettings["Env"] == "localhost")
                                {
                                    _log.Debug(
                                            $"Simulate sending quick link to Customer, due local type of Env https://beautyclick.sk/{q.Hash}.  " +
                                            $"This link will be sent via SMS in prod evn");
                                }
                                else
                                {
                                    var m = new MessageSendingHelper();
                                    if (user.IsCreated) // отправить СМС клиенту что его зарегистрировали
                                    {
                                        string message = $"{salonName} Vás objednal {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}, nabudúce sa objednajte sami on-line tu: www.beautyclick.sk/{q.Hash}";
                                        var res = m.SendSms(user.PhoneNumber, message);
                                        DB.MailingDb.AddUserSendMessage(json.masterId, 0, 0, 1, message, user.Id, user.PhoneNumber, null);
                                        _log.Debug($"Quick link successfully sent, sms info: {res}");
                                    }
                                    else // отправить СМС клиенту что его записали
                                    {
                                        if (!isMessagesDisabled)    // если пользователь НЕ отказался получать уведомления
                                        {
                                            /*var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(json.salonId);
                                            if (isSmsSalonPackage)  // если оплачен пакет с СМС
                                            {
                                            string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu: www.beautyclick.sk/{q.Hash}";
                                            var res = m.SendSms(user.PhoneNumber, message);
                                            DB.MailingDb.AddUserSendMessage(json.masterId, 0, 0, 1, message, null);
                                            _log.Debug($"Quick link successfully sent to user, sms info: {res}");*/
                                            //}
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception qe)
                        {
                            _log.Error("Error during saving and/or sending quick link", qe);
                        }
                    }
                }
                else
                {
                    _log.Debug($"Error book not inserted for UserId {booked.ClientId} Master {booked.MasterId} date {booked.Date} time {booked.From}");
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error during saving booking", ex);
            }
            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult SetWorkingTimeInCurrentDay(DateTime curDate, TimeSpan timeFrom, TimeSpan timeTo, long masterId)
        {
            var retResult = new ResultModel();

            try
            {
                DB.ScheduleDb.SetWorkingTimeForDay(curDate, timeFrom.Add(TimeSpan.FromHours(ClientBrowserUtcOffset)), timeTo.Add(TimeSpan.FromHours(ClientBrowserUtcOffset)), masterId);    // устанавливаем новое рабочее время в этот день
                _log.Info($"Master {masterId} succsessfully update working time for: {curDate.ToString("dd.MM.yyyy")}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error during updating working time on: {curDate.ToString("dd.MM.yyyy")} by master {masterId}", ex);
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }

            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult SetClientDescriptionByMaster(long masterId, long salonId, long clientId, string clientDescription)
        {
            try
            {
                if (clientDescription != "")
                {
                    DB.SalonDb.UpdateClientDescription(clientId, clientDescription, salonId);
                    _log.Info($"Master {masterId} succsessfully change customer with Id: {clientId} description to: {clientDescription}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during updating Customer with Id: {clientId} description to: {clientDescription} by master {masterId}", ex);
            }

            var retResult = new ResultModel();
            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult ChangeClientByMaster(ChangeClientJson json)
        {
            if (json == null)
            {
                _log.Error("error Change Client by Master json is null");
                return Json(new ResultModel() { IsSuccess = false });
            }

            try
            {
                if (json.clientName != "")
                {
                    DB.SalonDb.UpdateClientName(json.clientId, json.clientName, json.salonId);
                    _log.Info($"Master {json.masterId} succsessfully change customer name to: {json.clientName} with Id: {json.clientId}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during updating Customer name to: {json.clientName} with Id: {json.clientId} by master {json.masterId}", ex);
            }

            try
            {
                if (json.clientPhone == null || json.clientPhone == "")
                {
                    // телефон не меняется
                }
                else
                {
                    // если меняется телефон, нужно поменять телефон в уведомлениях
                    string oldPhone = DB.UserDb.GetUserPhoneById(json.clientId);
                    json.clientPhone = json.clientPhone.Replace("-", "");
                    DB.UserDb.ChangeCustomerPhone(json.clientId, json.clientPhone);
                    DB.MailingDb.UpdateRemindPhone(json.clientId, json.clientPhone);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during updating Customer phone to: {json.clientPhone} for user with Id: {json.clientId} by master {json.masterId}", ex);
            }

            var retResult = new ResultModel();
            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async System.Threading.Tasks.Task<ActionResult> AddSchedule(MasterAddScheduleJson json)
        {
            if (json == null)
            {
                _log.Error("error Master Add Schedule json is null");
                return Json(new ResultModel() {IsSuccess = false});
            }

            json.salonId = DB.SalonDb.GetSalonIdByMasterId(json.masterId);

            var retResult = new ResultModel();

            var masterId = MasterId();
            var isSolo = UserExtended.GetSoloMasterStatus(UserId);
            if ( (isSolo && json.masterId != masterId)  ||  (!isSolo && json.masterId!=UserId) )
            {
                if (!User.IsInRole(Role.SalonAdmin))
                {
                    _log.Debug(
                        $"Try hack, add schedule for dif Id, UserId:{UserId} model:  {JsonConvert.SerializeObject(json)}");
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                    return Json(retResult, JsonRequestBehavior.AllowGet);
                }
            }

            CultureInfo provider = CultureInfo.InvariantCulture;
            var date = DateTime.ParseExact(json.date, StringHelper.DateTimeParse, provider, DateTimeStyles.None);
            var now = DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
            if (date.Date < now.Date)
            {
                _log.Debug($"Master {masterId} tried to book for past date {date}  {json}");
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }
           
            var serviceDuration = DB.ServiceCategoryDb.GetServiceDuration(json.masterId, json.serviceId);
            if (serviceDuration <= 0)
            {
                retResult.Message =$"Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            var timeFrom = json.time.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
            var to = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration));
            var isBooked = DB.BookingDb.IsAvailableMasterSlot(json.masterId, date, timeFrom, to);
            if (isBooked)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! tento termín nieje dostupný, zvoľte iný prosím.";
                _log.Debug($"Booking time is occupated userId: {UserId} date {json.date}  time from utc {json.time}, time from {timeFrom}, time to{to}");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }
            
            UserSmallInfoForMaster user = null;
           
            user = GetOrCreateUser(json.clientPhone, json.clientName, masterId, json.customerId);
            if (user == null)
            {
                _log.Error($"Can't create/find user for booking, model: {json}. MasterId: {masterId}");
                retResult.IsSuccess = true;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }

            var booked = new BookedDto()
            {
                MasterId = json.masterId,
                SalonId = json.salonId,
                Added = DateTime.UtcNow,
                ClientId = user.Id,
                Date = date,
                From = timeFrom,
                To = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration)),
                ServiceId = json.serviceId,
                IsConfirmed = true
            };
            try
            {
                if (DB.BookingDb.InsertBooking(booked)>0)
                {
                    DB.UserDb.BindUserToSalon(user.Id, json.salonId, json.clientName);
                    
                    _log.Debug($"Booking successfully created by master: {masterId}  for service {json.serviceId} for user {user.Id} date {booked.Date}  time from {booked.From}, time to {booked.To}");
                    var hashLink = StringHelper.GenerateString(8);
                    var q = new QuickLoginLinkDto()
                    {
                        UserId = user.Id,
                        Added = DateTime.UtcNow,
                        Hash = hashLink
                    };
                    try
                    {
                        DB.UserDb.AddUserQuickLink(q);
                        _log.Debug("Quick link successfully saved");

                        var salonDto = DB.SalonDb.GetSalonInfo(json.salonId);
                        var first = salonDto.FirstOrDefault();
                        string salonName = "";
                        if (first != null)
                        {
                            salonName = first.Name;
                        }

                        var isMessagesDisabled = DB.UserDb.IsMessagesDisabled(user.Id); // если пользователь отказался получать уведомления

                        if (!string.IsNullOrEmpty(user.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                        {
                            if (!isMessagesDisabled)
                            {
                                var masterName = DB.MasterDb.GetMasterShortInfo(json.masterId, DB.MasterDb.GetOwnerByMasterId(json.masterId), LangId).Name;
                                var FbBot = new FacebookBotController();
                                string token = ConfigurationManager.AppSettings["FbPageToken"];
                                string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu: www.beautyclick.sk/{q.Hash}";
                                await FbBot.SendNotify(token, user.FacebookUserId, message);
                                DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message, user.Id, user.FacebookUserId, null);
                            }
                        }
                        else if (!string.IsNullOrEmpty(user.ViberUserId))
                        {
                            if (!isMessagesDisabled)
                            {
                                var masterName = DB.MasterDb.GetMasterShortInfo(json.masterId, DB.MasterDb.GetOwnerByMasterId(json.masterId), LangId).Name;
                                string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu:";
                                var res = MessageHelper.SendMsgWithButton(message, user.ViberUserId, "Moje rezervácie", $"https://beautyclick.sk/{q.Hash}", 3);
                                DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message, user.Id, user.ViberUserId, null); // записать сообщение в базу [SendedMessages]
                            }
                        }
                        else // СМС
                        {
                            if (ConfigurationManager.AppSettings["Env"] == "localhost")
                            {
                                _log.Debug(
                                    $"Simulate sending quick link to Customer, due local type of Env https://beautyclick.sk/{q.Hash}. This link will be sent via SMS in prod evn");
                            }
                            else
                            {
                                var m = new MessageSendingHelper();
                                if (user.IsCreated) //СМС о записи новому клиенту
                                {
                                    string message = $"{salonName} Vás objednal {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}, nabudúce sa objednajte sami on-line tu: www.beautyclick.sk/{q.Hash}";
                                    var res = m.SendSms(user.PhoneNumber, message);
                                    DB.MailingDb.AddUserSendMessage(masterId, 0, 0, 1, message, user.Id, user.PhoneNumber, null); // записать сообщение в базу [SendedMessages]
                                    _log.Debug($"Quick link successfully sent, sms info: {res}");
                                }
                                else  //СМС о записи существующему клиенту
                                {
                                    if (!isMessagesDisabled)
                                    {
                                        /*var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(json.salonId);
                                        if (isSmsSalonPackage)
                                        {*/
                                            /*var masterName = DB.MasterDb.GetMasterShortInfo(json.masterId, DB.MasterDb.GetOwnerByMasterId(json.masterId), LangId).Name;
                                            //string message = $"{masterName} vás objednal na procedúru {json.serviceName} na {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")} www.beautyclick.sk/{q.Hash}";
                                            string message = $"{salonName} vás objednal na termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}, pridajte si salón do oblubenych a nabuduce sa objednajte on-line tu: www.beautyclick.sk/{q.Hash}";
                                            var res = m.SendSms(user.PhoneNumber, message);
                                            DB.MailingDb.AddUserSendMessage(masterId, 0, 0, 1, message, null);
                                            _log.Debug($"Quick link successfully sent to user, sms info: {res}");*/
                                        //}
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception qe)
                    {
                        _log.Error("Error during saving and/or sending quick link", qe);
                    }
                }
                else
                {
                    _log.Debug($"Error book not inserted for UserId {booked.ClientId} Master {booked.MasterId} date {booked.Date} time {booked.From}");
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error during saving booking",ex);
            }

            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public JsonResult FindUser(string phone)
        {
            phone = "+" + phone;
            phone = StringHelper.ReplacePhoneFunc(phone);
            var user = GetUserByPhone(phone.Replace("+421",""));
            var retVal = new CustomerSearchModel()
            {
                Id = 0,
                Name = "",
                isExist = false
            };

            if (user.Item1 >0)
            {
                retVal.Id = user.Item1;
                retVal.Name = user.Item2;
                retVal.isExist = true;
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult OpenSlot(long slotId, long masterId)
        {
            var retVal = new ResultModel();
            try
            {
                //DB.BookingDb.DeleteBooking(MasterId(), slotId);
                DB.BookingDb.DeleteBooking(masterId, slotId);
                _log.Info($"Master {UserId} successfully opened slot for booking {slotId}");
                retVal.IsSuccess = true;
                retVal.Message = "Dokončené!";
            }
            catch (Exception ex)
            {
                _log.Error($"Error during opening time slot by Id {slotId} for master {UserId}",ex);
            }

            return Json(retVal, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult PrepareToMove(long slot, string time, long masterId)  // подготовка записи к переносу
        {
            var retVal = new ResultModel();
            try
            {
                var times = time.Split('-');
                var xDate = new DateTime(2000, 1, 1);
                TimeSpan from = TimeSpan.Parse(times[0]).Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
                TimeSpan to= TimeSpan.Parse(times[1]).Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
                if (DB.BookingDb.UpdateBookingByMaster(slot, masterId, xDate, from, to))
                {
                    _log.Debug($"Slot {slot} successfully prepared for Moving by {UserId}");
                    retVal.IsSuccess = true;
                    retVal.Message = "Dokončené!";
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during preparing to Move slot {slot} for Master {masterId} by {UserId}",ex);
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MoveSlot(MoveSlotModel data, long masterId)   // завршение переноса записи мастером
        {
            var retVal = new ResultModel();
            try
            {
                var booked = DB.BookingDb.GetBooked(data.SlotId, masterId);
                if (booked == null)
                {
                    retVal.IsSuccess = false;
                    retVal.Message = "Chyba! Operáciu zopakujte neskôr";
                    _log.Error($"Error during moving booking {data.SlotId} on date {data.Date} time: {data.Time}");
                    return Json(retVal, JsonRequestBehavior.AllowGet);
                }
                var duration = (booked.To - booked.From).TotalMinutes;
                var from = data.Time.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
                var to = from.Add(TimeSpan.FromMinutes(duration));
                CultureInfo provider = CultureInfo.InvariantCulture;
                var date = DateTime.ParseExact(data.Date, StringHelper.DateTimeParse, provider, DateTimeStyles.None);
                string format = "MM.dd.yyyy";
                var changedDate = DateTime.ParseExact(data.changedDate, format, provider, DateTimeStyles.None);
                var isBooked = DB.BookingDb.IsAvailableMasterSlot(masterId, date, from, to);
                if (isBooked)
                {
                    retVal.IsSuccess = false;
                    retVal.Message = "Chyba! tento termín nieje dostupný, zvoľte iný prosím.";
                    return Json(retVal, JsonRequestBehavior.DenyGet);
                }

                if (DB.BookingDb.UpdateBookingByMaster(data.SlotId, masterId, date, from, to))
                {
                    _log.Info($"Master {UserId} successfully moved booking {data.SlotId} on date: {data.Date} and time: {data.Time}");
                    try
                    {
                        var customerForCancellation = DB.UserDb.GetCustomerForCancellation(masterId, data.SlotId);
                        if (customerForCancellation != null)
                        {
                            if(customerForCancellation.PhoneNumber[0] == '0')
                            {
                                // удалить текущее напоминание для клиента
                                DB.BookingDb.RemoveBookRemind(data.SlotId, customerForCancellation.Id);
                                _log.Info($"Reminder for book: {data.SlotId} user Id: {customerForCancellation.Id} was deleted");

                                // добавить напоминание клиенту о записи за 3 часа до начала
                                string msg = $"Pripomíname Vám termín {date:dd.MM} o {data.Time:hh\\:mm} v salóne {customerForCancellation.SalonName}. Váš odborník krasy {customerForCancellation.MasterName} sa na vás už teší";
                                TimeSpan timeRemindUtc = from.Add(TimeSpan.FromHours(-6));
                                DateTime dateUtc;
                                if (timeRemindUtc.TotalMinutes > 0)
                                {
                                    dateUtc = new DateTime(date.Year, date.Month, date.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);
                                }
                                else
                                {
                                    dateUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                                }

                                DateTime datetime07 = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 5, 0, 0);
                                if (DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно быть ночью (перенос на 20:00 предидущего дня)
                                {
                                    dateUtc = dateUtc.AddDays(-1);
                                    dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                                }

                                var bookId = DB.MailingDb.AddNewBookRemind(customerForCancellation.Id, masterId, data.SlotId, msg, customerForCancellation.ViberUserId, customerForCancellation.FacebookUserId, customerForCancellation.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);
                                _log.Info($"Reminder was added for book {data.SlotId} user Id: {customerForCancellation.Id}. Remind at time: {data.Time:hh\\:mm}");

                                if (!DB.UserDb.IsMessagesDisabled(customerForCancellation.Id)) // если пользователь не отказался получать уведомления
                                {
                                    /*string message = string.Format(ConfigurationManager.AppSettings["MessageForMovedByMaster"],
                                       customersForCancellation.SalonName,
                                       $"{data.Date}  {data.Time.ToString("hh\\:mm")}",
                                       $"{ConfigurationManager.AppSettings["WebSiteLink"]}/Home/MySchedule/");*/

                                    if (!string.IsNullOrEmpty(customerForCancellation.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                                    {
                                        var FbBot = new FacebookBotController();
                                        string token = ConfigurationManager.AppSettings["FbPageToken"];
                                        string message = $"Vážený zákazník, {customerForCancellation.SalonName} preniesol vašu rezerváciu z {changedDate:dd.MM} o {data.changedTime} na {date:dd.MM} o {data.Time:hh\\:mm}. www.beautyclick.sk/Home/MySchedule";
                                        FbBot.SendNotify(token, customerForCancellation.FacebookUserId, message);
                                        DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message, customerForCancellation.Id, customerForCancellation.FacebookUserId, null);
                                    }
                                    else if (!string.IsNullOrEmpty(customerForCancellation.ViberUserId)) // Viber message for client
                                    {
                                        string message = $"Vážený zákazník, {customerForCancellation.SalonName} preniesol vašu rezerváciu z {changedDate:dd.MM} o {data.changedTime} na {date:dd.MM} o {data.Time:hh\\:mm}";
                                        var res = MessageHelper.SendMsgWithButton(message, customerForCancellation.ViberUserId, "Moje rezervácie",
                                            $"https://beautyclick.sk/Home/MySchedule", 2);
                                        _log.Debug(res.Status == ErrorCode.Ok
                                            ? $"Message about move booking by Master sent to Client {customerForCancellation.Id} "
                                            : $"Error during sending message about moved book to Clint {customerForCancellation.Id} due {res.StatusMessage}");
                                        DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message, customerForCancellation.Id, customerForCancellation.ViberUserId, null);
                                    }
                                    else // СМС клиенту
                                    {
                                        //var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(DB.SalonDb.GetSalonIdByMasterId(masterId));
                                        //if (isSmsSalonPackage)  // если оплачен пакет с СМС
                                        //{
                                        /*var m = new MessageSendingHelper();
                                        string message = $"{customerForCancellation.SalonName} preniesol vašu rezerváciu z {changedDate:dd.MM} o {data.changedTime} na {date:dd.MM} o {data.Time:hh\\:mm}";
                                        var res = m.SendSms(customerForCancellation.PhoneNumber, message + $" www.beautyclick.sk/Home/MySchedule");
                                        DB.MailingDb.AddUserSendMessage(masterId, 0, 0, 1, message, customerForCancellation.Id, customerForCancellation.PhoneNumber, null);*/
                                        //}
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error during sending message about moved booking by Master {masterId}",ex);
                    }
                    retVal.IsSuccess = true;
                    retVal.Message = "Dokončené!";
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error during close time slot {data.Date} {data.Time} for master {masterId}",ex);
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult AddScheduleChangeReservation(MasterAddScheduleJson json, long curBookId)
        {
            if (json == null)
            {
                _log.Error("error Master Add Schedule json is null");
                return Json(new ResultModel() { IsSuccess = false });
            }
            var retResult = new ResultModel();
            /*var masterId = MasterId();
            var isSolo = UserExtended.GetSoloMasterStatus(UserId);
            if ((isSolo && json.masterId != masterId) || (!isSolo && json.masterId != UserId))
            {
                if (!User.IsInRole(Role.SalonAdmin))
                {
                    _log.Debug(
                        $"Try hack, add schedule for dif Id, UserId:{UserId} model:  {JsonConvert.SerializeObject(json)}");
                    retResult.IsSuccess = false;
                    retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                    return Json(retResult, JsonRequestBehavior.AllowGet);
                }
            }*/
            CultureInfo provider = CultureInfo.InvariantCulture;
            var date = DateTime.ParseExact(json.date, StringHelper.DateTimeParse, provider, DateTimeStyles.None);
            string format = "MM.dd.yyyy";
            var changedDate = DateTime.ParseExact(json.changedDate, format, provider, DateTimeStyles.None);
            var now = DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
            if (date.Date < now.Date)
            {
                _log.Debug($"Master {json.masterId} tried to book for past date {date}  {json}");
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Tento termín nieje dostupný, zvoľte iný prosím.";
                return Json(retResult, JsonRequestBehavior.AllowGet);
            }
            var serviceDuration = DB.ServiceCategoryDb.GetServiceDuration(json.masterId, json.serviceId);
            if (serviceDuration <= 0)
            {
                retResult.Message = $"Chyba! Operáciu zopakujte neskôr";
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            // Удаляем текущую запись (чтобы освободить окно)
            try
            {
                DB.BookingDb.DeleteBooking(json.masterId, curBookId);
                _log.Info($"Master {UserId} succsessfully canceled booking {curBookId}");
                DB.BookingDb.RemoveBookRemind(curBookId, (long)json.customerId); // удалить текущее напоминание
                _log.Info($"Reminder for book: {curBookId} user Id: {(long)json.customerId} was deleted");
            }
            catch (Exception ex)
            {
                _log.Error($"Error during deleting booking {curBookId} master {json.masterId}", ex);
            }

            var timeFrom = json.time.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
            var to = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration));
            var isBooked = DB.BookingDb.IsAvailableMasterSlot(json.masterId, date, timeFrom, to);
            if (isBooked)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Tento termín nieje dostupný, zvoľte iný prosím.";
                _log.Debug($"Booking time is occupated userId: {UserId} date {json.date} time from utc {json.time}, time from {timeFrom}, time to {to}");
                return Json(retResult, JsonRequestBehavior.DenyGet);
            }

            UserSmallInfoForMaster user = DB.UserDb.GetUserShortInfoForMasterByUserId(json.customerId.Value);
            var booked = new BookedDto()
            {
                MasterId = json.masterId,
                SalonId = json.salonId,
                Added = DateTime.UtcNow,
                ClientId = json.customerId,
                Date = date,
                From = timeFrom,
                To = timeFrom.Add(TimeSpan.FromMinutes(serviceDuration)),
                ServiceId = json.serviceId,
                MasterComment = json.masterComment,
                IsConfirmed = true
            };
            try
            {
                retResult.Id = DB.BookingDb.InsertBooking(booked);
                if (retResult.Id > 0)
                {
                    _log.Debug($"Booking successfully changed by master: {json.masterId} for service {json.serviceId} for user {json.customerId} date {booked.Date:dd.MM.yyyy}  time from: {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}, time to {booked.To.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}");

                    string salonName = "";
                    var salonDto = DB.SalonDb.GetSalonInfo(json.salonId);
                    var first = salonDto.FirstOrDefault();
                    if (first != null)
                    {
                        salonName = first.Name;
                    }
                    var masterName = DB.MasterDb.GetMasterShortInfo(json.masterId, DB.MasterDb.GetOwnerByMasterId(json.masterId), LangId).Name;

                    if(user.PhoneNumber[0] == '0')
                    {
                        // добавить напоминание клиенту о записи за 3 часа до начала
                        string msg = $"Pripomíname Vám termín {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm} v salóne {salonName}. Váš odborník krasy {masterName} sa na vás už teší";
                        TimeSpan timeRemindUtc = timeFrom.Add(TimeSpan.FromHours(-6));
                        DateTime dateUtc;
                        if (timeRemindUtc.TotalMinutes > 0)
                        {
                            dateUtc = new DateTime(date.Year, date.Month, date.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);
                        }
                        else
                        {
                            dateUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                        }

                        DateTime datetime07 = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 5, 0, 0);
                        if (DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно быть ночью (перенос на 20:00 предидущего дня)
                        {
                            dateUtc = dateUtc.AddDays(-1);
                            dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                        }

                        var bookId = DB.MailingDb.AddNewBookRemind(user.Id, json.masterId, retResult.Id, msg, user.ViberUserId, user.FacebookUserId, user.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);
                        _log.Info($"Reminder was added for book {retResult.Id} user Id: {user.Id}. Remind at time: {timeRemindUtc.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}");
                        try
                        {
                            var isMessagesDisabled = DB.UserDb.IsMessagesDisabled(user.Id); // если пользователь отказался получать уведомления
                            if (!isMessagesDisabled)
                            {
                                if (!string.IsNullOrEmpty(user.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                                {
                                    var FbBot = new FacebookBotController();
                                    string token = ConfigurationManager.AppSettings["FbPageToken"];
                                    string message = $"{salonName} zmenil vašu objednávku z {changedDate:dd.MM} o {json.changedTime} na {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}. Viac info tu: www.beautyclick.sk/Home/MySchedule";
                                    FbBot.SendNotify(token, user.FacebookUserId, message);
                                    DB.MailingDb.AddUserSendMessage(json.masterId, 1, 0, 0, message, user.Id, user.FacebookUserId, null);
                                }
                                else if (!string.IsNullOrEmpty(user.ViberUserId))   // Viber meassage to Client
                                {
                                    string message = $"{salonName} zmenil vašu objednávku z {changedDate:dd.MM} o {json.changedTime} na {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}";
                                    var res = MessageHelper.SendMsgWithButton(message, user.ViberUserId, "Moje rezervácie", $"https://beautyclick.sk/Home/MySchedule", 3);
                                    DB.MailingDb.AddUserSendMessage(json.masterId, 0, 1, 0, message, user.Id, user.ViberUserId, null);
                                }
                                else  // СМС клиенту об изменении записи салоном
                                {
                                    if (ConfigurationManager.AppSettings["Env"] == "localhost")
                                    {
                                        _log.Debug(
                                            $"Simulate sending message: {salonName} zmenil vašu objednávku z {json.changedDate} o {json.changedTime} na {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")} " +
                                            $"This link will be sent via SMS in prod evn");
                                    }
                                    else
                                    {
                                        //var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(json.salonId);
                                        //if (isSmsSalonPackage)  // если оплачен пакет с СМС
                                        //{
                                        /*var m = new MessageSendingHelper();
                                        string message = $"{salonName} zmenil vašu objednávku z {changedDate:dd.MM} o {json.changedTime} na {booked.Date:dd.MM} o {booked.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString("hh\\:mm")}. Viac info tu: www.beautyclick.sk/Home/MySchedule";
                                        var res = m.SendSms(user.PhoneNumber, message);
                                        DB.MailingDb.AddUserSendMessage(json.masterId, 0, 0, 1, message, user.Id, user.PhoneNumber, null);
                                        _log.Debug($"Message about change procedure by master successfully sent, sms info: {res}");*/
                                        //}
                                    }
                                }
                            }
                        }
                        catch (Exception qe)
                        {
                            _log.Error("Error during sending message", qe);
                        }
                    }
                }
                else
                {
                    _log.Debug($"Error book not inserted for UserId {booked.ClientId} Master {booked.MasterId} date {booked.Date} time {booked.From}");
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error during saving booking", ex);
            }
            retResult.IsSuccess = true;
            retResult.Message = "Dokončené!";
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult AproveCustomerBooking(long slotId, long masterId)
        {
            var retVal = new ResultModel();
            try
            {
                var customer = DB.UserDb.GetCustomerForCancellation(masterId, slotId);
                if (customer != null)
                {
                    if (DB.BookingDb.AproveBookingByMaster(slotId, masterId))
                    {
                        _log.Debug($"Slot {slotId} successfully aproved by {masterId}");

                        var book = DB.BookingDb.GetBookedById(slotId);
                        if (book != null)
                        {   // добавить напоминание клиенту о записи за 3 часа до начала
                            var realTimeFrom = book.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                            string msg = $"Pripomíname Vám termín {book.Date:dd.MM} o {realTimeFrom:hh\\:mm} v salóne {book.SalonName}. Váš odborník krasy {book.MasterName} sa na vás už teší";
                            TimeSpan timeRemindUtc = book.From.Add(TimeSpan.FromHours(-6));
                            DateTime dateUtc;
                            if (timeRemindUtc.TotalMinutes > 0)
                            {
                                dateUtc = new DateTime(book.Date.Year, book.Date.Month, book.Date.Day, timeRemindUtc.Hours, timeRemindUtc.Minutes, 0);
                            }
                            else
                            {
                                dateUtc = new DateTime(book.Date.Year, book.Date.Month, book.Date.Day, 0, 0, 0);
                            }

                            DateTime datetime07 = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 5, 0, 0);
                            if (DateTime.Compare(dateUtc, datetime07) < 0)   // напоминание должно прийти ночью (перенос на 20:00 предидущего дня)
                            {
                                dateUtc = dateUtc.AddDays(-1);
                                dateUtc = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 18, 0, 0);
                            }

                            var bookId = DB.MailingDb.AddNewBookRemind(book.ClientId.Value, book.MasterId, slotId, msg, customer.ViberUserId, customer.FacebookUserId, customer.PhoneNumber, dateUtc.Date, dateUtc.TimeOfDay);
                            _log.Info($"Reminder was added for book {slotId} user Id: {book.ClientId.Value}. Remind at time: {timeRemindUtc.Add(TimeSpan.FromHours(2)):hh\\:mm}");
                        }

                        retVal.IsSuccess = true;
                        retVal.Message = "Dokončené!";

                        if (!DB.UserDb.IsMessagesDisabled(customer.Id)) // если пользователь не отказался получать уведомления
                        {
                            string message = $"{customer.SalonName} potvrdil vašu objednávku na {customer.Date:dd.MM} o {customer.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)):hh\\:mm}";

                            if (!string.IsNullOrEmpty(customer.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                            {
                                var FbBot = new FacebookBotController();
                                string token = ConfigurationManager.AppSettings["FbPageToken"];
                                FbBot.SendNotify(token, customer.FacebookUserId, message + ". Viac info tu: www.beautyclick.sk/Home/MySchedule");
                                DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message, customer.Id, customer.FacebookUserId, null);
                            }
                            else if (!string.IsNullOrEmpty(customer.ViberUserId))   // Сообщение клиенту на Вайбер
                            {
                                var res = MessageHelper.SendMsgWithButton(message + " a teší sa na vašu návštevu!", customer.ViberUserId, "Moje rezervácie", $"https://beautyclick.sk/Home/MySchedule", 2);
                                _log.Debug(res.Status == ErrorCode.Ok ? $"Message about aproving book by Master sent to Client {customer.Id} " : $"Error during sending message about aproving book to Clint {customer.Id} due {res.StatusMessage}");
                                DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message, customer.Id, customer.ViberUserId, null);
                            }
                            else // СМС клиенту о подтверждении записи мастером
                            {
                                /*var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(book.SalonId.Value);
                                if (isSmsSalonPackage)  // если оплачен пакет с СМС
                                {*/
                                    var m = new MessageSendingHelper();
                                    var res = m.SendSms(customer.PhoneNumber, message + $" www.beautyclick.sk/Home/MySchedule");
                                    DB.MailingDb.AddUserSendMessage(masterId, 0, 0, 1, message, customer.Id, customer.PhoneNumber, null);
                                    _log.Debug($"Message about aproving procedure by master successfully sent, sms info: {res}");
                                //}
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                retVal.IsSuccess = false;
                _log.Error($"Error during aproving slot: {slotId} by master {masterId} ", ex);
                retVal.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult DeleteBookingBeforeChange(long slotId, long masterId)
        {
            var retVal = new ResultModel();
            try
            {
                var customerForCancellation = DB.UserDb.GetCustomerForCancellation(masterId, slotId);

                DB.BookingDb.DeleteBooking(masterId, slotId);
                retVal.IsSuccess = true;
                _log.Info($"Master {UserId} succsessfully canceled booking {slotId}");

                if (customerForCancellation != null)
                {
                    DB.BookingDb.RemoveBookRemind(slotId, customerForCancellation.Id); // удалить текущее напоминание
                    _log.Info($"Reminder for book: {slotId} user Id: {customerForCancellation.Id} was deleted");
                }

                retVal.Message = "Dokončené!";
            }
            catch (Exception ex)
            {
                retVal.IsSuccess = false;
                _log.Error($"Error during deleting booking {slotId} master {masterId}", ex);
                retVal.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult CancelCustomerBooking(long slotId, long masterId /*CancelBookingModel data*/)
        {
            var retVal = new ResultModel();
            try
            {
                var today = DateTime.UtcNow.AddHours(ClientBrowserUtcOffsetToNormal);
                //var customersForCancellation = DB.UserDb.GetCustomerForCancellation(MasterId(), data.Id);
                var customersForCancellation = DB.UserDb.GetCustomerForCancellation(masterId, slotId);
                if (customersForCancellation != null)
                {
                    if(customersForCancellation.Date < today.Date)
                    {
                        _log.Debug($"Master {masterId} tried to cancell booked item for past date {slotId}  {customersForCancellation.Date}");
                        retVal.IsSuccess = false;
                        retVal.Message = "Chyba! Operáciu zopakujte neskôr";
                        return Json(retVal, JsonRequestBehavior.AllowGet);
                    }
                    string message = string.Format(ConfigurationManager.AppSettings["MessageForCancellationByMaster"],
                        customersForCancellation.Date.ToString("dd.MM"),
                        customersForCancellation.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString(@"hh\:mm"),
                        customersForCancellation.SalonName); //, customersForCancellation.ServiceName,                        customersForCancellation.MasterName);
                    DB.BookingDb.DeleteBooking(masterId, slotId);
                    retVal.IsSuccess = true;
                    _log.Info($"Master {UserId} succsessfully canceled booking {slotId} for customer {customersForCancellation.CustomerName}");
                    retVal.Message = "Dokončené!";

                    //DB.BookingDb.RemoveBookRemind(customersForCancellation.BookedId, customersForCancellation.Id);
                    DB.BookingDb.RemoveBookRemind(slotId, customersForCancellation.Id); // удалить напоминание
                    _log.Info($"Reminder for book: {slotId} user Id: {customersForCancellation.Id} was deleted");

                    var salonId = DB.SalonDb.GetSalonIdByMasterId(masterId);
                    
                    if(customersForCancellation.PhoneNumber[0] == '0')
                    {
                        try
                        {
                            if (!DB.UserDb.IsMessagesDisabled(customersForCancellation.Id)) // если пользователь не отказался получать уведомления
                            {
                                if (!string.IsNullOrEmpty(customersForCancellation.FacebookUserId)) // отправка сообщения клиенту на Messenger Facebook
                                {
                                    var FbBot = new FacebookBotController();
                                    string token = ConfigurationManager.AppSettings["FbPageToken"];
                                    FbBot.SendNotify(token, customersForCancellation.FacebookUserId, message + $" www.beautyclick.sk/Salon/{salonId}");
                                    DB.MailingDb.AddUserSendMessage(masterId, 1, 0, 0, message + $" www.beautyclick.sk/Salon/{salonId}", customersForCancellation.Id, customersForCancellation.FacebookUserId, null);
                                }
                                else if (!string.IsNullOrEmpty(customersForCancellation.ViberUserId))   // отправка сообщения клиенту на Viber
                                {
                                    var res = MessageHelper.SendMsgWithButton(message, customersForCancellation.ViberUserId, "Vybrať nový termín", $"https://beautyclick.sk/Salon/{salonId}", 2);
                                    _log.Debug(res.Status == ErrorCode.Ok
                                        ? $"Cancelled message sent to Client {customersForCancellation.Id} ({customersForCancellation.CustomerName})"
                                        : $"Error during sending message about canceled book to Clint {customersForCancellation.Id} due {res.StatusMessage}");
                                    DB.MailingDb.AddUserSendMessage(masterId, 0, 1, 0, message + $" www.beautyclick.sk/Salon/{salonId}", customersForCancellation.Id, customersForCancellation.ViberUserId, null);
                                }
                                else  // отправка СМС клиенту об удалении его записи мастером
                                {
                                    if (ConfigurationManager.AppSettings["Env"] == "localhost")
                                    {
                                        _log.Debug($"Simulate sending cancel sms to Customer");
                                    }
                                    else
                                    {
                                        /*var isSmsSalonPackage = DB.SalonDb.IsSmsAllowed(salonId);
                                        if (isSmsSalonPackage)  // если оплачен пакет с СМС
                                        {*/
                                        /*var m = new MessageSendingHelper();
                                        var res = m.SendSms(customersForCancellation.PhoneNumber, message + $" www.beautyclick.sk/Salon/{salonId}");
                                        DB.MailingDb.AddUserSendMessage(masterId, 0, 0, 1, message + $" www.beautyclick.sk/Salon/{salonId}", null);
                                        _log.Debug($"SMS about canceled booking successfully sent to client {customersForCancellation.Id} ({customersForCancellation.CustomerName}), sms info: {res}");*/
                                        //}
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Error during sending cancellation message to customer {customersForCancellation.CustomerName} ", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                retVal.IsSuccess = false;
                _log.Error($"Error during deleting booking {slotId} master {masterId}", ex);
                retVal.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult CloseSlot(CloseSlotModel data)
        {
            var retVal = new ResultModel();
            try
            {
                if (data.MasterId == null || data.MasterId == 0)
                {
                    _log.Error($"Error during close  time slot {data.Date} {data.Time} due masterId is null");
                    retVal.IsSuccess = false;
                    retVal.Message = "Chyba! Operáciu zopakujte neskôr";
                    return Json(retVal, JsonRequestBehavior.AllowGet);
                }
                CultureInfo provider = CultureInfo.InvariantCulture;
                var date = DateTime.ParseExact(data.Date, StringHelper.DateTimeParse, provider, DateTimeStyles.None);
                var from = data.Time.Add(TimeSpan.FromHours(ClientBrowserUtcOffset));
                var to = from.Add(TimeSpan.FromMinutes(15));
                //var id=  DB.BookingDb.AddCloseSlot(MasterId(), data.SalonId,date,from,to);
                var id = DB.BookingDb.AddCloseSlot(data.MasterId.Value, data.SalonId, date, from, to);
                _log.Info($"Master {data.MasterId.Value} succsessfully closed slot  on date {data.Date} time {data.Time} for booking");
                retVal.IsSuccess = true;
                retVal.Message = "Dokončené!";
            }
            catch (Exception ex)
            {
                _log.Error($"Error during close  time slot {data.Date} {data.Time}  for master {data.MasterId}",ex);
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult CreateTempProcedure(int duration, long masterId, string name)
        {
            var retVal = new ResultModel();
            try
            {
                var salonId = DB.SalonDb.GetSalonIdByMasterId(masterId);
                var ownerId = User.GetSoloMasterStatus() ? OwnerId() : DB.MasterDb.GetOwnerByMasterId(masterId);
                var specialityId = 61;
                var categoryId = 54;
                var price = 0;
                //var serviceId = (long)335;

                // Добавление записи в таблицу [Service]
                var serviceId = DB.ServiceCategoryDb.AddNewService(name, "", price, duration, specialityId, salonId, ownerId);
                retVal.Id = serviceId;

                // образец: var servResult = DB.MasterDb.AddMasterServices(newServices, model.SalonId, model.OwnerId, true); 
                // Добавление записи в таблицу [Master_Service]
                var masterServiceId = DB.MasterDb.AddNewService(masterId, serviceId, specialityId, categoryId, duration, price);
                if (masterServiceId > 0)
                {
                    _log.Debug($"Temp procedure {serviceId} successfully created by {masterId}");
                    retVal.IsSuccess = true;
                    retVal.Message = "Dokončené!";
                    retVal.Id = serviceId;
                }
            }
            catch (Exception ex)
            {
                retVal.IsSuccess = false;
                _log.Error($"Error during creating temp procedure by master {masterId} ", ex);
                retVal.Message = "Chyba! Operáciu zopakujte neskôr";
            }
            return Json(retVal, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public ActionResult GetNotConfirmedReservations(long masterid)
        {
            var retVal = new MasterScheduleWithMasterInfoModel(ClientBrowserUtcOffset)
            {
                Schedule = DB.MasterDb.GetMasterNotConfirmedBooked(masterid)
                //Schedule = DB.MasterDb.GetMasterScheduleAndBooked(masterid, DB.MasterDb.GetOwnerByMasterId(masterid), month, day)
            };
            if (retVal.Schedule != null)
            {
                foreach (var s in retVal.Schedule)
                {
                    if (s.BookedFrom.HasValue && s.BookedTo.HasValue)
                    {
                        if (s.BookedFrom.Value > new TimeSpan(0, 0, 0))
                        {
                            s.BookedFrom = s.BookedFrom.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                        }
                        if (s.BookedTo.Value < new TimeSpan(23, 59, 0))
                        {
                            s.BookedTo = s.BookedTo.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                        }
                        if (string.IsNullOrEmpty(s.CustomerAvatar))
                        {
                            s.CustomerAvatar = ConfigurationManager.AppSettings["AvatarDefaultUrl"];
                        }
                    }
                }
            }
            return PartialView("_MasterScheduleNotConfirmed", retVal);
        }
        

        [HttpGet]
        public ActionResult GetMasterDay(long masterid, int day, int month, int year)
        {
            var retVal = new MasterScheduleWithMasterInfoModel(ClientBrowserUtcOffset)
            {
                //Schedule = DB.MasterDb.GetMasterScheduleAndBooked(masterid, OwnerId(), month, day),
                Schedule = DB.MasterDb.GetMasterScheduleAndBooked(masterid, DB.MasterDb.GetOwnerByMasterId(masterid), year, month, day)
            };
            if (retVal.Schedule != null)
            {
                //whole day busy.
                if (retVal.Schedule.Count == 1)
                {
                    var s = retVal.Schedule.FirstOrDefault();
                    if (s?.BookedFrom != null && s.BookedFrom.Value == new TimeSpan(0, 0, 0) && s.BookedTo.HasValue && s.BookedTo.Value == new TimeSpan(23, 59, 0))
                    {
                        retVal.Schedule.Add(new MasterScheduleAndBookedDto()
                        {
                            ServiceId = null,
                            Month = (short) month,
                            Day = (short) day,
                            Year = year,
                            BookedFrom = retVal.From,
                            BookedTo = retVal.To,
                        });
                    }
                }
                else 
                {
                    foreach (var s in retVal.Schedule)
                    {
                        if (s.BookedFrom.HasValue && s.BookedTo.HasValue)
                        {
                            if (s.BookedFrom.Value > new TimeSpan(0, 0, 0))
                            {
                                s.BookedFrom = s.BookedFrom.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                            }
                            if (s.BookedTo.Value < new TimeSpan(23, 59, 0))
                            {
                                s.BookedTo = s.BookedTo.Value.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal));
                            }
                            if (string.IsNullOrEmpty(s.CustomerAvatar))
                            {
                                s.CustomerAvatar = ConfigurationManager.AppSettings["AvatarDefaultUrl"];
                            }
                        }
                    }
                }
            }
            return PartialView("_MasterSchedule", retVal);
        }

        public ActionResult ScheduleLoad(long id, int month, int day)
        {
            if (month < 1 || month > 12 || day <= 0 || day >= 32)
            {
                return View("StopCheating");
            }

            var retVal = new MasterScheduleModel()
            {
                Schedule = DB.MasterDb.GetMasterScheduleAndBooked(id, OwnerId(), 2022, month, day), // добавил 2022 год вручную!!!
                DayNavigation = new DayNavigation()
                {
                    Link = "/Master/ScheduleLoad/" + id + "/",
                    Now = new DateTime(DateTime.UtcNow.Year, month, day),
                    Lang = (Language) LangId
                }
            };
            return PartialView("_SheduleDay", retVal);
        }


        public JsonResult CancelDays(string json, long masterId)
        {
            var retResult = new ResultModel()
            {
                IsSuccess = true
            };
            try
            {
                List<DaysJson> days = JsonConvert.DeserializeObject<List<DaysJson>>(json);
                List<DateTime> dates = new List<DateTime>();
                dates = days.Select(a => new DateTime(a.Year, a.Month, a.Day, 0, 0, 0)).ToList();
                var customersForCancellation = DB.UserDb.GetCustomersForCancellation(masterId, dates);
                if (days.Any())
                {
                    foreach (var d in days)
                    {
                        var date = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0);
                        try
                        {
                            var res=DB.BookingDb.CancelAllAndBookDay(masterId, date);
                            if (res!=null && !string.IsNullOrEmpty(res.ErrorMessage))
                            {
                                _log.Error($"Error during remove bookings and make busy day for {masterId} on {date}");
                            }
                            else
                            {
                                _log.Info($"All bookings of master {masterId} on date {date} successfully deleted");
                                retResult.Message ="Dokončené!";
                            }
                        }
                        catch (Exception ex)
                        {
                            retResult.IsSuccess = false;
                            retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                            _log.Error($"Error during remove bookings and make busy day {UserId} {date}",ex);
                        }
                    }
                }
                var bookedId = new List<long>();
                if (customersForCancellation.Any())
                {
                    var salonId = DB.SalonDb.GetSalonIdByMasterId(masterId);
                    List<MessageToSendDto> msgToSendDtos = new List<MessageToSendDto>();
                    foreach (var c in customersForCancellation)
                    {
                        if (!DB.UserDb.IsMessagesDisabled(c.Id)) // если пользователь не отказался получать уведомления
                        {
                            msgToSendDtos.Add(new MessageToSendDto()
                            {
                                IsSent = false,
                                Message = string.Format(ConfigurationManager.AppSettings["MessageForCancellationByMaster"], c.Date.ToString("dd.MM"), c.From.Add(TimeSpan.FromHours(ClientBrowserUtcOffsetToNormal)).ToString(@"hh\:mm"), c.SalonName/*, c.ServiceName, c.MasterName*/), //date, time, salon name
                                MessageType = (short)Shared.Enums.MessageType.CustomerBookingCanceledByMaster,
                                ReceiverId = c.Id,
                                ReceiverName = c.CustomerName,
                                ReceiverPhone = c.PhoneNumber,
                                ReceiverViber = c.ViberUserId,
                                ReceiverFacebook = c.FacebookUserId,
                                SenderId = masterId,
                                ScheduleTime = DateTime.UtcNow,
                                SenderName = c.MasterName,
                                SalonId = salonId
                            });
                        }
                        _log.Debug($"Master {masterId} successfully deleted booking {c.BookedId} for customer {c.CustomerName} {c.Id}");
                    }
                    retResult.IsSuccess = true;
                    retResult.Message = "Dokončené!";
                    DB.MailingDb.AddMessages(msgToSendDtos);
                }
                bookedId = customersForCancellation.Select(a => a.BookedId).ToList();
                _log.Debug($"{bookedId.Count} messages for cancelling booking");

                try
                {
                    var res = DB.BookingDb.RemoveBookRemind(bookedId);
                    _log.Debug($"{res} booking messages was deteled");
                }
                catch(Exception exBookMsg)
                {
                    _log.Error($"Error during deleting booking reminder messages ", exBookMsg);
                }
            }
            catch (Exception ex)
            {
                retResult.IsSuccess = false;
                retResult.Message = "Chyba! Operáciu zopakujte neskôr";
                _log.Error($"Error during cancellation booking by master  {UserId} days : {json}",ex);
            }
            return Json(retResult, JsonRequestBehavior.AllowGet);
        }


        [AllowAnonymous]
        public ActionResult Login()
        {
            TempData["loginacctype"] = "master";
            return RedirectToAction("Login", "Account");
        }


        private Tuple<long,string> GetUserByPhone(string customerPhone)
        {
            customerPhone = StringHelper.ReplacePhoneFunc(customerPhone);
            return DB.UserDb.GetUserIdNameForMaster(customerPhone);
        }


        private UserSmallInfoForMaster GetOrCreateUser(string customerPhone, string customerName,long masterId,long? userId)
        {
            customerPhone = StringHelper.ReplacePhoneFunc(customerPhone);
            var userInfo = DB.UserDb.GetUserShortInfoForMaster(customerPhone);
            if (userId != null && userId > 0 && customerPhone != "")
            {
                return DB.UserDb.GetUserShortInfoForMasterByUserId(userId.Value);
            }
            if (userInfo == null || customerPhone == "")
            {
                try
                {
                    _log.Debug($"User not exist phone : {customerPhone}. Creating user by Master {masterId}");

                    if(customerPhone == "")
                    {
                        //нужно создать уникальный номер телефона
                        long nextUniquePhoneNumber = DB.UserDb.GetLastUniqueClient() + 1;
                        customerPhone = nextUniquePhoneNumber.ToString();
                    }

                    var userName = customerPhone + "_customer@vsesaloni.online";
                    var pin = StringHelper.GeneratePin(6);
                    string confirmCode = StringHelper.GeneratePin(0);
                    // var userLocation= 
                    var user = new ApplicationUser
                    {
                        UserName = userName,
                        Email = userName,
                        Name = customerName,
                        AccountType = (short) AccountType.Customer,
                        PhoneNumber = customerPhone,
                        Registered = DateTime.UtcNow,
                        SecurityCode = StringHelper.GenerateString(10),
                        RegisteredIp = HttpContext.Request.UserHostAddress,
                        Country = (short) UserCountryGeneral,
                        City = 1,
                        PackageType = (short) PackagesType.Customer,
                        UtcShift = ClientBrowserUtcOffset,
                        PhoneConfirmCode = confirmCode,
                        PhoneNumberConfirmed = true,
                        IsByMasterRegistered = true,
                        AvatarUrl = ConfigurationManager.AppSettings["AvatarDefaultUrl"]
                    };

                    var result = UserManager.Create(user, pin);

                    if (result.Succeeded)
                    {
                        _log.Info($"Customer {user.Name} succsessfully registered by Master {UserId}");
                        UserManager.AddToRole(user.Id, Helper.Role.Customer);
                        return new UserSmallInfoForMaster()
                        {
                            Id = user.Id,
                            PhoneNumber = user.PhoneNumber,
                            ViberUserId = "",
                            IsCreated = true
                        };
                    }
                    else
                    {
                        _log.Error($"Error during customer registration  {customerName}. by Master, errors :  {string.Join(",", result.Errors.ToList())}");
                        var msg = "";
                        if (result.Errors.FirstOrDefault(a => a.Contains("is already taken")) != null)
                        {
                            msg = "Toto telefónne číslo sa už používa";
                        }
                        else
                        {
                            msg = result.Errors.FirstOrDefault();
                        }
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"Error during customer registration by Mater  {UserId}  new customer phonenumber: {customerPhone}", ex);
                    return null;
                }
            }
            else
            {
                userInfo.IsCreated = false;
                return userInfo;
            }
        }
    }
}