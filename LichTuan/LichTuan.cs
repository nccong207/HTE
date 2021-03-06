using System;
using System.Collections.Generic;
using System.Text;
using Plugins;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using CDTLib;

namespace LichTuan
{
    public class LichTuan:IC
    {
        private List<InfoCustom> _lstInfo = new List<InfoCustom>();
        #region IC Members

        public void Execute(DataRow drMenu)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.WorkingDirectory = "Plugins\\HTA\\LichTuan";
            psi.FileName = "LichTuan_IC.exe";

            string nlv = Config.GetValue("NamLamViec").ToString();
            string cn = Config.GetValue("MaCN").ToString();
            string sttd = Config.GetValue("STTD").ToString();
            string pn = Config.GetValue("PackageName").ToString();
            string cnn = Config.GetValue("DataConnection").ToString();

            psi.Arguments = nlv + " " + cn + " " + sttd + " \"" + pn + "\" \"" + cnn + "\"";
            Process.Start(psi);
        }

        public List<InfoCustom> LstInfo
        {
            get { return _lstInfo; }
        }

        #endregion

        public LichTuan()
        {
            InfoCustom ic = new InfoCustom(1011, "Xếp lịch học", "Quản lý học viên");
            _lstInfo.Add(ic);
        }
    }
}
