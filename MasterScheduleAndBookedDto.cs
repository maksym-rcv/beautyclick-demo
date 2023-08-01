using System;
using System.Collections.Generic;
using System.Text;

namespace Dto.Entities.Custom
{
    public class MasterScheduleAndBookedDto
    {
        public System.DateTime Date { get; set; }
        public short Day { get; set; }
        public short Month { get; set; }
        public int Year { get; set; }
        public System.TimeSpan From { get; set; }
        public System.TimeSpan To { get; set; }
        public Nullable<long> BookedId { get; set; }
        public Nullable<long> ClientId { get; set; }
        public string CustomerAvatar { get; set; }
        public Nullable<long> MasterId { get; set; }
        public Nullable<long> SalonId { get; set; }
        public Nullable<System.DateTime> BookedDate { get; set; }
        public Nullable<System.TimeSpan> BookedFrom { get; set; }
        public Nullable<System.TimeSpan> BookedTo { get; set; }
        public Nullable<System.DateTime> BookedAdded { get; set; }
        public Nullable<long> ServiceId { get; set; }
        public string ClientComment { get; set; }
        public string MasterComment { get; set; }
        public Nullable<bool> IsCanceled { get; set; }
        public Nullable<bool> IsFinished { get; set; }
        public Nullable<bool> isOff { get; set; }
        public Nullable<bool> IsConfirmed { get; set; }
        public Nullable<bool> IsCoffeeBreak { get; set; }
        public string SalonOwnerComment { get; set; }

        public string ServiceName { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public  bool? IsByMasterRegistered { get; set; }
        public string CustomerNameForMaster { get; set; }
        public string CustomerDescription { get; set; }
    }
}
