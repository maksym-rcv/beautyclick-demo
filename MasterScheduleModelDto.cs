using System;
using System.Collections.Generic;
using System.Text;

namespace Dto.Entities.Custom
{

    public class MasterScheduleModelDto
    {
        public long MasterId { get; set; }
        public System.DateTime? Date { get; set; }
        public short? Day { get; set; }
        public short? Month { get; set; }
        public int? Year { get; set; }
        public System.TimeSpan? From { get; set; }
        public System.TimeSpan? To { get; set; }
    }
    //GetMasterScheduleDay
    //GetMasterScheduleMonth
    //GetMasterInfo
}
