using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using CDTLib;

namespace DangKyLopICC2
{
    public partial class FrmHocPhi : DevExpress.XtraEditors.XtraForm
    {
        DataRow _drDKLop;
        public FrmHocPhi(DataRow drDKLop)
        {
            InitializeComponent();
            dateEdit1.DateTime = DateTime.Today;
            if (drDKLop["TConlai"].ToString() != "")
                calcEdit1.Value = Convert.ToDecimal(drDKLop["TConlai"]);
            _drDKLop = drDKLop;
        }
        public DateTime ngaythu;
        public string httt;
        public decimal sotien;
        public bool isMuaBT;

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            string msg1 = "";
            msg1 = comboBoxEdit1.SelectedItem == DBNull.Value ? "Cần chọn hình thức thanh toán.":"";
            msg1 = calcEdit1.EditValue == DBNull.Value || Convert.ToDecimal(calcEdit1.EditValue) < 0? "Số tiền nhập không hợp lệ.":"";
            msg1 = dateEdit1.EditValue == DBNull.Value?"Ngày thu không hợp lệ.":"";
            if (msg1 != "")
            {
                XtraMessageBox.Show(msg1,Config.GetValue("PackageName").ToString());
                return;
            }
            ngaythu = Convert.ToDateTime(dateEdit1.EditValue);
            sotien = Convert.ToDecimal(calcEdit1.EditValue);
            httt = comboBoxEdit1.SelectedItem.ToString();
            isMuaBT = ckMuaBT.Checked;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ckMuaGT_CheckedChanged(object sender, EventArgs e)
        {
            if (!Convert.ToBoolean(_drDKLop["IsMuaBT"]) && ckMuaBT.Checked)
            {
                XtraMessageBox.Show("Học viên này không mua giáo trình, không thể thu tiền giáo trình",
                    Config.GetValue("PackageName").ToString());
                ckMuaBT.Checked = false;
            }
        }
    }
}