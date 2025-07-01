using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Parser
{
    public interface IParser
    {
        uint Parse(string msgLine, long time);
    }
}
