using System;
using System.Collections.Generic;
using System.Text;
using CDTDatabase;
using CDTLib;
using DevExpress;
using DevExpress.XtraEditors;
using Plugins;
using System.Windows.Forms;
namespace HVBaoLuu
{
    public class HVBaoLuu:IC
    {
        #region IC Members

        private List<InfoCustom> _lstInfo = new List<InfoCustom>();

        public HVBaoLuu()
        {
            InfoCustom ic = new InfoCustom(1053, "Cho học viên bảo lưu", "Quản lý học viên");
            _lstInfo.Add(ic);
        }

        public void Execute(System.Data.DataRow drMenu)
        {
            int menuID = Int32.Parse(drMenu["MenuPluginID"].ToString());
            if (_lstInfo[0].CType == ICType.Custom && _lstInfo[0].MenuID == menuID)
            {
                Form main = null;
                foreach (Form fr in Application.OpenForms)
                    if (fr.IsMdiContainer)
                        main = fr;
                frmHVBL frm = new frmHVBL();
                frm.Text = drMenu["MenuName"].ToString();
                if (main == null)
                {
                    frm.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    frm.ShowDialog();
                }
                else
                {
                    frm.MdiParent = main;
                    frm.Show();
                }
            }
        }

        public List<InfoCustom> LstInfo
        {
            get { return _lstInfo; }
        }

        #endregion
    }
}
