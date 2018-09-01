using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Windows.Forms;
using Plugins;
using CDTDatabase;
using CDTLib;
using DevExpress.XtraLayout;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using FormFactory;


namespace ChonHocVien
{
    public class ChonHocVien:ICControl
    {
        InfoCustomControl info = new InfoCustomControl(IDataType.MasterDetailDt);
        DataCustomFormControl data;
        LayoutControl lcMain;
        GridView gvMain; //gridview detail
        GridView gvDSHV; //gridview trong report preview
        ReportPreview rpDSHV;
        DataRow drCur;
        #region ICControl Members

        public void AddEvent()
        {
            lcMain = data.FrmMain.Controls.Find("lcMain",true) [0] as LayoutControl;
         
            //Tạo nút chọn học viên đề nghị khai giảng
            SimpleButton btnChonHocVien = new SimpleButton();
            btnChonHocVien.Name = "btnChonHocVienDNKG";
            btnChonHocVien.Text = "Chọn học viên";
            LayoutControlItem lci = lcMain.AddItem("", btnChonHocVien);
            lci.Name = "btnChonHocVien";
            btnChonHocVien.Click += new EventHandler(btnChonHocVien_Click);
        }

        void btnChonHocVien_Click(object sender, EventArgs e)
        {
                        
            gvMain = (data.FrmMain.Controls.Find("gcMain", true)[0] as GridControl).MainView as GridView;
            if (!gvMain.Editable)
            {
                XtraMessageBox.Show("Để chọn học viên vui lòng chuyển sang chế độ sửa hoặc thêm phiếu.", 
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            drCur = (data.BsMain.Current as DataRowView).Row;
            if (drCur["MaCD"] == DBNull.Value) //bắt buộc chọn cấp độ trước để lọc danh sách học viên
            {
                XtraMessageBox.Show("Vui lòng chọn cấp độ để lấy danh sách!",
                    Config.GetValue("PackageName").ToString());
                return;
            }
            Config.NewKeyValue("@MaCD", drCur["MaCD"]);
            // nếu đã duyệt thì không cho chọn học viên 
            if (drCur["TinhTrang"].ToString() == "Đã duyệt")
            {
                XtraMessageBox.Show("Danh sách học viên đã được duyệt, không thể chọn thêm học viên !",
                    Config.GetValue("PackageName").ToString());
                return;
            }
            rpDSHV = FormFactory.FormFactory.Create(FormType.Report, "1687") as ReportPreview;
            if (rpDSHV == null)
                return;
            gvDSHV = (rpDSHV.Controls.Find("gridControlReport", true)[0] as GridControl).MainView as GridView;
            SimpleButton btnXuLy = rpDSHV.Controls.Find("btnXuLy", true)[0] as SimpleButton;
            btnXuLy.Click += new EventHandler(btnXuLy_Click);
            rpDSHV.WindowState = FormWindowState.Maximized;
            rpDSHV.ShowDialog();
        }

        void btnXuLy_Click(object sender, EventArgs e)
        {
            DataTable dtDSHV = (gvDSHV.DataSource as DataView).Table;
            dtDSHV.AcceptChanges();
            DataRow[] drDSHV = dtDSHV.Select("Chọn = 1");
            if (drDSHV.Length == 0)
            {
                XtraMessageBox.Show("Vui lòng chọn học viên đề nghị khai giảng.",
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            rpDSHV.Close();
            DataTable dtDTDNKH = (data.BsMain.DataSource as DataSet).Tables[1];

            DataView dv = new DataView(dtDTDNKH);
            drCur = (data.BsMain.Current as DataRowView).Row;
            dv.RowFilter = "MTKGID = '" + drCur["MTKGID"].ToString() + "'";
            dtDTDNKH = dv.ToTable(); 

            foreach (DataRow dr in drDSHV)
            {
                if (!CheckExists(dtDTDNKH, dr["ID"].ToString(),(int)dr["HVTVID"]))
                    continue;
                gvMain.AddNewRow();
                gvMain.UpdateCurrentRow();
                gvMain.SetFocusedRowCellValue(gvMain.Columns["DTKGID"], Guid.NewGuid());
                gvMain.SetFocusedRowCellValue(gvMain.Columns["MTKGID"], drCur["MTKGID"]);
                gvMain.SetFocusedRowCellValue(gvMain.Columns["DTDKID"], dr["ID"]);
                gvMain.SetFocusedRowCellValue(gvMain.Columns["HVTVID"], dr["HVTVID"]);
                gvMain.SetFocusedRowCellValue(gvMain.Columns["MaCD"], dr["MaCD"]);
            }
            drCur["SiSo"] = gvMain.DataRowCount;                
        }        
        bool CheckExists(DataTable dt,string DTDKLopID,int HVTVID)
        {
            if (DTDKLopID != "")
            {
                if (dt.Select("DTDKID = '" + DTDKLopID + "'").Length > 0)
                    return false;
            }
            else
            {
                if (dt.Select("DTDKID is null and HVTVID = " + HVTVID).Length > 0)
                    return false;
            }
            return true;
        }
        public DataCustomFormControl Data
        {
            set { data = value; }
        }

        public InfoCustomControl Info
        {
            get { return info; }
        }

        #endregion
    }
}

