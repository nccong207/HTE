using System;
using System.Collections.Generic;
using System.Text;

using CDTLib;
using Plugins;
using CDTDatabase;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Columns;

namespace HVNghiHocICR
{
    public class HVNghiHocICR:ICReport
    {
        Database db = Database.NewDataDatabase();
        DataCustomReport _data;
        InfoCustomReport _info = new InfoCustomReport(IDataType.Report);
        GridView gvMain;
        GridControl gcMain;
        DataView dvMain;
        DataTable dtMain;
        DataTable dtMainOriginal; /* Dùng cho việc kiểm tra học viên đã được nghỉ học và ngày nghỉ học 
                                   * trước khi bấm xử lý giống với sau khi bấm xử lý
                                   */ 
                                   
        #region ICReport Members

        public DataCustomReport Data
        {
            set { _data = value; }
        }

        public InfoCustomReport Info
        {
            get { return _info; }
        }


        public void Execute()
        {
            gvMain = (_data.FrmMain.Controls.Find("gridControlReport", true)[0] as GridControl).MainView as GridView;
            gvMain.CustomRowCellEdit += new CustomRowCellEditEventHandler(gvMain_CustomRowCellEdit);
            gvMain.BestFitColumns();
            SimpleButton btnXL = _data.FrmMain.Controls.Find("btnXuLy", true)[0] as SimpleButton;
            btnXL.Click += new EventHandler(btnXL_Click);
        }

        void gvMain_CustomRowCellEdit(object sender, CustomRowCellEditEventArgs e)
        {
            if (e.Column.FieldName != "Ngày nghỉ") return;

            RepositoryItemDateEdit riDateEdit = new RepositoryItemDateEdit();
            riDateEdit.Name = "riteNgayNghi";
            riDateEdit.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTime;
            riDateEdit.Mask.EditMask = "dd/MM/yyyy";
            riDateEdit.Mask.UseMaskAsDisplayFormat = true;

            GridView gv = sender as GridView;
            if (gv == null) return;
            e.RepositoryItem = riDateEdit;
                    
        }   

        void btnXL_Click(object sender, EventArgs e)
        {
            dvMain = gvMain.DataSource as DataView;
            if (dvMain == null) return;
            dtMain = dvMain.Table.GetChanges(DataRowState.Modified);
            if (dtMain == null) return;

            string TenHV = "";// Dùng khi Show MessageBox thông báo ngày nghỉ rỗng
            string sql = "";

            // Lấy giá trị mặc định dưới db trước khi xử lý
            if (Convert.ToBoolean(Config.GetValue("@IsHienHV9994")))
            {
                sql = string.Format(@"EXEC SP_NGHIHOC @IsHienHVNghi = '{0}', @MaLop = '{1}', @TenHV = '{2}', @MODE = 'BAOCAO'",
                                    Config.GetValue("@IsHienHV9994"),
                                    Config.GetValue("@MaLop9994"),
                                    Config.GetValue("@HVTVID9994"));
                dtMainOriginal = db.GetDataTable(sql);
                sql = "";
           }
            
            for (int i = 0; i < dtMain.Rows.Count; i++)
            {
                // Kiểm tra nếu cho học viên nghỉ học mà không nhập ngày nghỉ
                if (Convert.ToBoolean(dtMain.Rows[i]["Chọn"].ToString()) &&
                    dtMain.Rows[i]["Ngày nghỉ"].ToString() == "")
                {
                    if (TenHV == "")
                        TenHV = "'" + dtMain.Rows[i]["Họ tên"].ToString() + "'";
                    else
                        TenHV += "; '" + dtMain.Rows[i]["Họ tên"].ToString() + "'";
                }

                // So sánh giá trị mặc định dưới db và khi bấm xử lý nếu bằng nhau thì không xử lý
                if (Convert.ToBoolean(Config.GetValue("@IsHienHV9994")) && dtMainOriginal != null)
                {
                    DataRow[] drOriginal = dtMainOriginal.Select(
                                              string.Format("[Chọn] = '{0}' AND HVID = '{1}' AND [Ngày nghỉ] = '{2}'",
                                              dtMain.Rows[i]["Chọn"],
                                              dtMain.Rows[i]["HVID"], dtMain.Rows[i]["Ngày nghỉ"]));
                    if (dtMainOriginal != null && drOriginal.Length > 0)
                        continue;
                }
                
                // Xử lý nghỉ học
                if (Convert.ToBoolean(dtMain.Rows[i]["Chọn", DataRowVersion.Current].ToString())
                    && (dtMain.Rows[i]["Chọn", DataRowVersion.Current].ToString() != dtMain.Rows[i]["Chọn", DataRowVersion.Original].ToString() 
                    || (dtMain.Rows[i]["Ngày nghỉ", DataRowVersion.Current].ToString() != dtMain.Rows[i]["Ngày nghỉ", DataRowVersion.Original].ToString())))
                {
                    sql += string.Format(@" EXEC SP_NGHIHOC @HVTVID = '{0}', @HVID = '{1}', 
                                            @NgayNghi = '{2}', @TinhTrang = N'{3}', 
                                            @UserName = '{4}', @MODE = 'DUYET'", 
                                            dtMain.Rows[i]["HVTVID"], dtMain.Rows[i]["HVID"],
                                            dtMain.Rows[i]["Ngày nghỉ"], dtMain.Rows[i]["TinhTrang"],
                                            Config.GetValue("UserName"));
                }
                // Xử lý hủy nghỉ học
                if (!Convert.ToBoolean(dtMain.Rows[i]["Chọn", DataRowVersion.Current].ToString())
                    && dtMain.Rows[i]["Chọn",DataRowVersion.Current].ToString() != dtMain.Rows[i]["Chọn",DataRowVersion.Original].ToString())
                {
                    sql += string.Format(@" EXEC SP_NGHIHOC @HVTVID = '{0}', @HVID = '{1}', 
                                            @NgayNghi = '{2}', @TinhTrang = N'{3}', 
                                            @UserName = '{4}', @MODE = 'HUYDUYET'",
                             dtMain.Rows[i]["HVTVID"], dtMain.Rows[i]["HVID"],
                             dtMain.Rows[i]["Ngày nghỉ"], dtMain.Rows[i]["TinhTrang"],
                             Config.GetValue("UserName"));
                }
            }
            if (TenHV != "")
            {
                XtraMessageBox.Show("Phải nhập ngày nghỉ học cho học viên : " + TenHV,
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if(string.IsNullOrEmpty(sql) || sql == "") return;
            if (db.UpdateByNonQuery(sql))
            {
                // Sau khi xử lý, Refresh lại báo cáo
                dvMain.RowFilter = string.Format("Chọn = '{0}'", Config.GetValue("@IsHienHV9994"));

                GridControl gcMain = _data.FrmMain.Controls.Find("gridControlReport", true)[0] as GridControl;
                gcMain.DataSource = dvMain;
                gcMain.RefreshDataSource();
            }
            

           
        }
        
        #endregion
    }
}
