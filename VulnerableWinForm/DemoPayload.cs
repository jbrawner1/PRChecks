using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VulnerableWinForm
{
    [Serializable]
    public class DemoPayload
    {
        public string Message { get; set; }
        public override string ToString() => Message;
    }
}
