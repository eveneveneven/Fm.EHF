using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STARTLibrary.accesspointService;

namespace Fm.EHF
{
    /// <summary>
    /// Result returned from service
    /// </summary>
    public class SendResult
    {
        public string MessageId { get; set; }
        public CreateResponse1 Response { get; set; }
        public TimeSpan TimeSpent { get; set; }
    }
}
