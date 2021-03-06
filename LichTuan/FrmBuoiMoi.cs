using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace LichTuan
{
    public partial class FrmBuoiMoi : DevExpress.XtraEditors.XtraForm
    {
        public string MaLH;
        public int NCa;
        public DateTime Ngay;
        public bool Copy = false;
        public FrmBuoiMoi(DataTable dtLop, DataTable dtCa, DateTime ngay, int nCa)
        {
            InitializeComponent();

            DataTable dtLH = dtLop.Copy();
            dtLH.DefaultView.RowFilter = "TenLop is not null";
            dtLH.PrimaryKey = new DataColumn[] { dtLH.Columns["MaLop"] };
            dtLH.DefaultView.Sort = "MaLop";
            gluLH.Properties.DataSource = dtLH;
            gluLH.Properties.ValueMember = "MaLop";
            gluLH.Properties.DisplayMember = "MaLop";
            gluLH.Properties.View.Columns["NgayBDKhoa"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gluLH.Properties.View.Columns["NgayBDKhoa"].DisplayFormat.FormatString = "dd/MM/yyyy";
            gluLH.Properties.View.Columns["NgayKTKhoa"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gluLH.Properties.View.Columns["NgayKTKhoa"].DisplayFormat.FormatString = "dd/MM/yyyy";
            gluLH.Properties.PopupFormMinSize = new Size(600, 600);
            gluLH.Properties.View.BestFitColumns();


            gluCa.Properties.DataSource = dtCa;
            gluCa.Properties.DisplayMember = "MaCa";
            gluCa.Properties.ValueMember = "MaCa";
            gluCa.Properties.View.Columns["TGBD"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gluCa.Properties.View.Columns["TGBD"].DisplayFormat.FormatString = "HH:mm";
            gluCa.Properties.View.Columns["TGKT"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gluCa.Properties.View.Columns["TGKT"].DisplayFormat.FormatString = "HH:mm";
            gluCa.Properties.View.BestFitColumns();
            gluCa.EditValue = dtCa.Rows[nCa]["MaCa"];

            deNgay.DateTime = ngay;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (gluLH.EditValue == null || gluLH.EditValue.ToString() == "")
            {
                XtraMessageBox.Show("Vui lòng chọn lớp học");
                return;
            }
            MaLH = gluLH.EditValue.ToString();
            DataTable dtCa = gluCa.Properties.DataSource as DataTable;
            NCa = dtCa.Rows.IndexOf(dtCa.Rows.Find(gluCa.EditValue));
            Ngay = deNgay.DateTime;
            Copy = ceCopy.Checked;
            this.DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}