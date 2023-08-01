using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using DAL.DbModels;
using DAL.DbModels.CustomModels.Master;
using Dto.Entities;
using Dto.Entities.Custom;
using Shared.Db;
using Shared.Enums;

namespace DBManager.Master
{
    public sealed class MasterDbManager
    {
        private static volatile MasterDbManager _instance;
        private static readonly object SyncObj = new object();

        public static MasterDbManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncObj)
                    {
                        _instance = new MasterDbManager();
                    }
                }
                return _instance;
            }
        }

        public List<ServiceDto> GetServiceBySpeciality(long specialityId, long salonId, long ownerId, bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                if (!General.AccessCheckDbManager.Instance.IsSalonAdmin(salonId, ownerId))
                {
                    throw new AccessViolationException("Access denied to salon!");
                }
            }

            using (var db = new VseSaloni())
            {
                var res = db.Database.SqlQuery<DAL.DbModels.Service>(
                    "ServiceBySpeciality @SpecialityId,@SalonId",
                    new SqlParameter("@SpecialityId", specialityId),
                    new SqlParameter("@SalonId", salonId)).ToList();
                return Dto.MapperFromDb.Mapper.Map<List<DAL.DbModels.Service>, List<ServiceDto>>(res);
            }
        }

        public List<DateTime> GetLatestMasterDatesInCalendar(long masterId,long ownerId,bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                    if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
                    {
                        throw new AccessViolationException("Access denied to master!");
                    }
            }
            string sql = @"
            SELECT CAST(day(00) AS VARCHAR(2)) +'-' + CAST(MONTH(date) AS VARCHAR(2)) + '-' + CAST(YEAR(date) AS VARCHAR(4)) AS Date
            FROM[Booked]
            WHERE MasterId =  "+ masterId+
            " GROUP BY CAST(day(00) AS VARCHAR(2)) + '-' + CAST(MONTH(date) AS VARCHAR(2)) + '-' + CAST(YEAR(date) AS VARCHAR(4))";
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<DateTime>(sql).ToList();
            }
        }

        public TimeSpan GetWorkingTimeFrom(long masterId, DateTime date)
        {
            string sql = $"SELECT [Booked].[To] FROM [Booked] WHERE [Booked].[MasterId]={masterId} AND [Booked].[Date] like '{date:yyyy-MM-dd}' AND [Booked].[From] like '00:00:00'";    //2022-06-24
            using(var db = new VseSaloni())
            {
                return db.Database.SqlQuery<TimeSpan>(sql).FirstOrDefault();
            }
        }

        public TimeSpan GetWorkingTimeTo(long masterId, DateTime date)
        {
            string sql = $"SELECT [Booked].[From] FROM Booked WHERE [Booked].[MasterId]={masterId} AND [Booked].[Date] like '{date:yyyy-MM-dd}' AND [Booked].[To] like '23:59:00'";    //2022-06-24
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<TimeSpan>(sql).FirstOrDefault();
            }
        }

        public DateTime GetLatestMasterDateInCalendar(long masterId, long ownerId, bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
                {
                    throw new AccessViolationException("Access denied to master!");
                }
            }

            string sql = $"SELECT TOP (1) [Date] FROM [Booked]   where MasterId={masterId}   order by Date desc;";
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<DateTime>(sql).FirstOrDefault();
            }
        }

        public List<ImageDto> GetMasterImages(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<ImageDto>(@"SELECT [Id],[OwnerId],[FullImgUrl],[ImageType],[RelatedObjectType],[AltText],
                                        [IsMainPhoto],[AddedUtc],[RelatedObjectId]   FROM Image Where RelatedObjectId=" + masterId).ToList();
            }
        }

        public ServiceDto GetServiceById(long salonId, long serviceId, long ownerId, bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                if (!General.AccessCheckDbManager.Instance.IsSalonAdmin(salonId, ownerId))
                {
                    throw new AccessViolationException("Access denied to master!");
                }
            }
            using (var db = new VseSaloni())
            {
                var res = db.Database.SqlQuery<DAL.DbModels.Service>(
                    "ServiceById @ServiceId,@SalonId",
                    new SqlParameter("@ServiceId", serviceId),
                    new SqlParameter("@SalonId", salonId)).FirstOrDefault();

                return Dto.MapperFromDb.Mapper.Map<DAL.DbModels.Service, ServiceDto>(res);
            }
        }

        public void DeletePreviousMasterService(long masterId)
        {
            using (var db = new VseSaloni())
            {
                db.Database.ExecuteSqlCommand(@" delete FROM Master_Service where MasterId = " + masterId.ToString());
            }
        }

        public long GetOwnerByMasterId(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<long>("SELECT  [OwnerId] FROM [Master_SalonAdmin] where MasterId = " + masterId).FirstOrDefault();
            }
        }

        public long GetMasterNewBookesMessagesCount(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<int>("SELECT COUNT(Id) FROM [InternalMessage] WHERE [To]=" + masterId + " AND [Subject]='New Client Reservation' AND [BookedId] IS NOT NULL and [ReadUtc] IS NULL").FirstOrDefault();
            }
        }

        public long GetMasterDeletedBookesMessageCount(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<int>("SELECT COUNT(Id) FROM [InternalMessage] WHERE [To]=" + masterId + " AND [Subject]='Deleted Client Reservation' AND [BookedId] IS NOT NULL and [ReadUtc] IS NULL").FirstOrDefault();
            }
        }

        public long GetMasterMovedBookesMessageCount(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<int>("SELECT COUNT(Id) FROM [InternalMessage] WHERE [To]=" + masterId + " AND [Subject]='Moved Client Reservation' AND [BookedId] IS NOT NULL and [ReadUtc] IS NULL").FirstOrDefault();
            }
        }


        public GetInfoAfterTempBookCompleted GetMasterByBookId(long bookId)
        {
            using (var db = new VseSaloni())
            {
                string sql = @"select AspNetUsers.ViberUserId,Booked.MasterId,Booked.SalonId
                                            from Booked 
                                            inner join AspNetUsers on AspNetUsers.Id= Booked.MasterId
                                            where Booked.Id="+bookId;
                return db.Database.SqlQuery<GetInfoAfterTempBookCompleted>(sql).FirstOrDefault();
            }
        }

        public long GetMasterIdByOwnerId(long ownerId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<long>("SELECT  top(1) [MasterId] FROM [Master_SalonAdmin] where OwnerId=" + ownerId).FirstOrDefault();
            }
        }

        public long GetServiceSpecialityById(long serviceId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<long>("SELECT SpecialityId FROM Service WHERE Id=" + serviceId).FirstOrDefault();
            }
        }

        public bool AddMasterSpeciality(long masterId, long specialityId, long salonId, long ownerId, bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
                {
                    throw new AccessViolationException("Access denied to salon!");
                }
            }

            using (var db = new VseSaloni())
            {
                db.Master_Speciality.Add(new Master_Speciality()
                {
                    SalonId = salonId,
                    MasterId = masterId,
                    SpecialityId = specialityId
                });
                db.SaveChanges();
                return true;
            }
        }

        public bool AddMasterToSalon(long masterId, long specialityId, long salonId, long ownerId, bool isValidationRequired)
        {
            if (isValidationRequired)
            {
                if (!General.AccessCheckDbManager.Instance.IsSalonAdmin(salonId, ownerId))
                {
                    throw new AccessViolationException("Access denied to salon!");
                }
            }

            using (var db = new VseSaloni())
            {
                db.Master_Salon.Add(new Master_Salon()
                {
                    SalonId = salonId,
                    MasterId = masterId,
                    Speciality = specialityId,
                    Added = DateTime.UtcNow,
                    IsActive = true
                });
                db.SaveChanges();
                return true;
            }
        }


        public long AddNewService(long masterId, long serviceId, long specialityId, long categoryId, int duration, int price)
        {
            using (var db = new VseSaloni())
            {
                var newService = new DAL.DbModels.Master_Service()
                {
                    MasterId = masterId,
                    ServiceId = serviceId,
                    Price = price,
                    Duration = duration,
                    SpecialityId = specialityId,
                    CategoryId = categoryId
                };
                db.Master_Service.Add(newService);
                db.SaveChanges();
                return newService.Id;
            }
        }

        public void DeleteMasterServiceBySpeciality(long masterId, long specialityId, long ownerId,
            bool isValidationRequired)
        {

            if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
            {
                throw new AccessViolationException("Access denied to salon!");
            }

            using (var db = new VseSaloni())
            {
                db.Database.ExecuteSqlCommand(
                    $"Delete from Master_Service where MasterId={masterId} and SpecialityId={specialityId}");
            }

        }

        public void AddMasterOwnerRelation(long masterId, long ownerId)
        {
            using (var db = new VseSaloni())
            {
                db.Master_SalonAdmin.Add(new Master_SalonAdmin()
                {
                    OwnerId = ownerId,
                    MasterId = masterId
                });
                db.SaveChanges();
            }
        }

        public List<long> GetMastersIdsBySalonId(long salonId)
        {
            using (var db = new VseSaloni())
            {
                return db.Master_Salon.Where(a => a.SalonId == salonId).Select(a => a.MasterId).ToList();
            }
        }

        public List<MasterShortDto> GetMasterList(long salonId, long ownerId, bool isValidationRequired)
        {
            using (var db = new VseSaloni())
            {
                string sql = @"select [Master].Id,[Master].Name,[Master].AvatarUrl as Avatar,
                                [Master].IsActive,
                                Master_salon.SalonId,
                                 [Specialty].[Name] as Speciality,
                                Salon.Name as SalonName
                                from AspNetUsers as [Master]
                                inner join Master_salon  on Master_salon.MasterId = Master.Id 
                                inner join Salon on Salon.Id= Master_Salon.SalonId 
                                inner join Master_Speciality on Master_Speciality.MasterId= Master.Id
                                inner join Specialty on [Specialty].Id= Master_Speciality.SpecialityId
                                where Master_salon.SalonId= @SalonId ";
                var lst = db.Database.SqlQuery<MasterShortDbModel>(sql, new SqlParameter("@SalonId", salonId)).ToList();

                return Dto.MapperFromDb.Mapper.Map<List<MasterShortDbModel>, List<MasterShortDto>>(lst);
            }
        }
        
        public List<MasterScheduleModelDto> GetMasterMonthSchedule(long masterId, long ownerId, int month)
        {
            using (var db = new VseSaloni())
            {
                List<MasterScheduleDbModel> res;
                if (masterId == ownerId)
                {
                    res = db.Database.SqlQuery<MasterScheduleDbModel>(
                        "GetMasterScheduleMonthNoOwner @MasterId,@Month",
                        new SqlParameter("@MasterId", masterId),
                        new SqlParameter("@Month", month)

                    ).ToList();
                }
                else
                {
                     res = db.Database.SqlQuery<MasterScheduleDbModel>(
                        "GetMasterScheduleMonth @MasterId,@OwnerId,@Month",
                        new SqlParameter("@MasterId", masterId),
                        new SqlParameter("@OwnerId", ownerId),
                        new SqlParameter("@Month", month)

                    ).ToList();
                }

                return Dto.MapperFromDb.Mapper.Map<List<MasterScheduleDbModel>, List<MasterScheduleModelDto>>(res);
            }
        }

        public List<MasterScheduleModelDto> GetMasterDaySchedule(long masterId, long ownerId, int month, int day)
        {
            using (var db = new VseSaloni())
            {
                var res = db.Database.SqlQuery<MasterScheduleDbModel>("GetMasterScheduleDay @MasterId,@OwnerId,@Month",
                    new SqlParameter("@MasterId", masterId),
                    new SqlParameter("@OwnerId", ownerId),
                    new SqlParameter("@Month", month),
                    new SqlParameter("@Day", day)
                ).ToList();

                return Dto.MapperFromDb.Mapper.Map<List<MasterScheduleDbModel>, List<MasterScheduleModelDto>>(res);
            }
        }


        public MasterShortInfoDto GetMasterShortInfo(long masterId, long ownerId, short langId)
        {
            MasterShortInfoDto retVal;
            using (var db = new VseSaloni())
            {
                MasterShortInfoDbModel res;
                if (masterId == ownerId)
                {
                    res = db.Database.SqlQuery<MasterShortInfoDbModel>(
                        "GetMasterInfoForMaster @MasterId",
                        new SqlParameter("@MasterId", masterId)).FirstOrDefault();
                }
                else
                {
                    res = db.Database.SqlQuery<MasterShortInfoDbModel>(
                        "GetMasterInfo @MasterId,@OwnerId",
                        new SqlParameter("@MasterId", masterId),
                        new SqlParameter("@OwnerId", ownerId)).FirstOrDefault();
                }
                retVal = Dto.MapperFromDb.Mapper
                    .Map<MasterShortInfoDbModel, MasterShortInfoDto>(res);
            }

            return retVal;
        }

        private List<MasterScheduleAndBookedDto> GetMasterScheduleAndBookedWithoutOwner(long masterId, int year, int month, int day)
        {
            List<MasterScheduleAndBookedDto> retVal;
            using (var db = new VseSaloni())
            {
                var res = db.Database.SqlQuery<MasterScheduleAndBookedDbModel>(
                    "GetMasterScheduleAndBookedByDayWO @MasterId,@Year,@Month,@Day",
                    new SqlParameter("@MasterId", masterId),
                    new SqlParameter("@Year", year), 
                    new SqlParameter("@Month", month),
                    new SqlParameter("@Day", day)
                ).ToList();

                retVal = Dto.MapperFromDb.Mapper
                    .Map<List<MasterScheduleAndBookedDbModel>,
                        List<MasterScheduleAndBookedDto>>(res);
            }

            return retVal;
        }

        public int GetMasterNotConfirmedBookedCount(long masterId)
        {
            using (var db = new VseSaloni())
            {
                return db.Database.SqlQuery<int>("SELECT COUNT(Id) FROM [Booked] where IsConfirmed=0 AND ServiceId IS NOT NULL AND MasterId=" + masterId).FirstOrDefault();
            }
        }


        public List<MasterScheduleAndBookedDto> GetMasterNotConfirmedBooked(long masterId)
        {
            List<MasterScheduleAndBookedDto> retVal;
            using (var db = new VseSaloni())
            {
                string sql =
                    @"  SELECT 
                          Booked.Date,
                          Booked.[From] as BookedFrom,
                          Booked.[To] as BookedTo,
                          Booked.Id as BookedId, 
                          Booked.Added as BookedAdded,
                          Booked.ClientComment,
                          Booked.ClientId,
                          Booked.IsCanceled,
                          Booked.IsCoffeeBreak,
                          Booked.IsFinished,
                          Booked.IsConfirmed,
                          Booked.MasterComment,
                          Booked.MasterId,
                          Booked.SalonId,
                          Booked.ServiceId,
                          Booked.SalonOwnerComment,  Service.Name as ServiceName,
                          AspNetUsers.Name as CustomerName,
                          AspNetUsers.AvatarUrl as CustomerAvatar,
                          AspNetUsers.PhoneNumber as CustomerPhone,
                          AspNetUsers.IsByMasterRegistered

                        FROM Booked
                        left join Service on Booked.ServiceId = Service.Id
                        left join AspNetUsers on Booked.ClientId = AspNetUsers.Id
                        WHERE Booked.MasterId=@MasterId and Booked.ClientId IS NOT NULL and Booked.IsConfirmed=0 ORDER BY Booked.Date ASC, BookedFrom ASC";
                var res = db.Database.SqlQuery<MasterScheduleAndBookedDbModel>(sql, new SqlParameter("@MasterId", masterId)).ToList();
                retVal = Dto.MapperFromDb.Mapper.Map<List<MasterScheduleAndBookedDbModel>, List<MasterScheduleAndBookedDto>>(res);
            }
            return retVal;
        }

        public List<MasterScheduleAndBookedDto> GetMasterScheduleAndBooked(long masterId, long ownerId, int year, int month, int day)
        {
            List<MasterScheduleAndBookedDto> retVal;
            if (masterId == ownerId)
            {
                return GetMasterScheduleAndBookedWithoutOwner(masterId, year, month, day);
            }

            if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
            {
                throw new AccessViolationException("Access denied to Master!");
            }
            
            using (var db = new VseSaloni())
            {
                var res = db.Database.SqlQuery<MasterScheduleAndBookedDbModel>(
                    "GetMasterScheduleAndBookedByDayWO @MasterId,@Year,@Month,@Day",
                    new SqlParameter("@MasterId", masterId),
                    new SqlParameter("@Year", year),
                    new SqlParameter("@Month", month),
                    new SqlParameter("@Day", day)
                ).ToList();

                retVal = Dto.MapperFromDb.Mapper.Map<List<MasterScheduleAndBookedDbModel>, List<MasterScheduleAndBookedDto>>(res);
            }
            return retVal;
        }

        public List<MasterWithSalonFullInfo> GetMasterServices(long masterId)
        {
            using(var db = new VseSaloni())
            {
                var dbRes = db.Database.SqlQuery<MasterWithSalonFullInfo>(@"SELECT
                    Specialty.Name as MasterSpecialty,
                    Service.Id as ServiceId, Service.Name as ServiceName, Service.Description as ServiceDescription,
                    Master_Service.Duration as ServiceDuration, Master_Service.Price as ServicePrice
                    FROM Master_Salon
                    inner join Master_Service on Master_Service.MasterId=Master_Salon.MasterId
                    inner join Service on Master_Service.ServiceId = Service.Id
                    inner join Specialty on Master_Service.SpecialityId = Specialty.Id 
                    WHERE Master_Salon.MasterId=" + masterId).ToList();
                return dbRes;
            }
        }

        public List<MasterWithSalonFullInfo> GetMasterWithSalonInfo(long masterId)
        {
            using (var db = new VseSaloni())
            {
                var dbRes = db.Database.SqlQuery<MasterWithSalonFullInfo>("SelectMasterWithSalonFullInfo @MasterId",
                    new SqlParameter("@MasterId", masterId)).ToList();
                return dbRes;
            }
        }

        public List<MasterInfoWithServiceDto> GetMasterInfoWithService(long masterId, long ownerId)
        {
            if (masterId != ownerId)
            {
                if (!General.AccessCheckDbManager.Instance.IsMasterOwner(masterId, ownerId))
                {
                    throw new AccessViolationException("Access denied to salon!");
                }
            }

            using (var db = new VseSaloni())
            {
                try
                {
                    var res = db.Database.SqlQuery<MasterInfoWithServiceDto>("GetMasterInfoWithService_Upd @MasterId",
                        new SqlParameter("@MasterId", masterId)).ToList();
                    return res;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public void UpdateMaster(long id, string name, string photo)
        {
            using (var db = new VseSaloni())
            {
                var user=db.AspNetUsers.FirstOrDefault(a => a.Id == id);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(photo))
                    {
                        user.AvatarUrl = photo;
                    }
                    user.Name = name;
                    db.SaveChanges();
                }
            }
        }
        public void UpdateMasterPhone(long id, string phone)
        {
            using (var db = new VseSaloni())
            {
                var user = db.AspNetUsers.FirstOrDefault(a => a.Id == id);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(phone))
                    {
                        user.PhoneNumber = phone;
                    }
                    db.SaveChanges();
                }
            }
        }

        public void UpdateSoloAdmin(long id, string name, string photo)
        {
            using (var db = new VseSaloni())
            {
                var user = db.AspNetUsers.FirstOrDefault(a => a.Id == id);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(photo))
                    {
                        user.AvatarUrl = photo;
                    }

                    if (!string.IsNullOrEmpty(user.Name))
                    {
                        user.Name = name;
                    }

                    db.SaveChanges();
                }
            }
        }
        public void UpdateMasterConfirmNeed(long id, bool isConfirmNeed)
        {
            using (var db = new VseSaloni())
            {
                var user = db.AspNetUsers.FirstOrDefault(a => a.Id == id);
                if (user != null)
                {
                    user.IsConfirmNeed = isConfirmNeed;
                    db.SaveChanges();
                }
            }
        }

        public void UpdateMasterDescription(long id, string description)
        {
            using (var db = new VseSaloni())
            {
                var user=db.AspNetUsers.FirstOrDefault(a => a.Id == id);
                if (user != null)
                {
                    user.Description=description;
                    db.SaveChanges();
                }
            }
        }

        public string GetMasterViber(long masterId)
        {
            using (var db = new VseSaloni())
            {
                var exist=db.AspNetUsers.FirstOrDefault(a => a.Id == masterId && a.AccountType == (short) AccountType.Master);
                if (exist != null)
                {
                    return exist.ViberUserId;
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Get Masters without schedule on next months after date. Ignored day. 
        /// </summary>
        /// <param name="date"></param>
        /// <param name="msgHistoryType"></param>
        /// <returns> Uniq id is combination of daymonth and year </returns>
        public List<MasterInfoForRemindAboutSchedule> GetMastersWithOutSchedule(DateTime date,short msgHistoryType)
        {
            using(var db= new VseSaloni())
            {
                return db.Database.SqlQuery<MasterInfoForRemindAboutSchedule>("GetMastersWithOutScheduleForNextMonth @Date, @MessageHistoryType ",
                    new SqlParameter("@Date", date), new SqlParameter("@MessageHistoryType", msgHistoryType)).ToList();
            }
        }
    }
}