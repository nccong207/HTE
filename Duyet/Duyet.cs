using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Windows.Forms;
using Plugins;
using DevExpress.XtraEditors;
using CDTLib;
using CDTDatabase;

namespace Duyet
{
    public class Duyet:ICData
    {
        DataCustomData data;
        InfoCustomData info;
        Database db = Database.NewDataDatabase();
        DataRow drMaster;
        DataView dv_DTDNKG;
        bool isDeleteDetail;
        string colTinhTrang = "TinhTrang";

        #region ICData Members
        public Duyet() 
        {
            info = new InfoCustomData(IDataType.MasterDetailDt);
        }
        public DataCustomData Data
        {
            set { data = value; }
        }

        public void ExecuteAfter()
        {
            //drMaster = data.DsDataCopy.Tables[0].Rows[data.CurMasterIndex];
            //isDeleteDetail = GetDeletedChangesDetail() != null ? true : false;
            ////bool isDuyet = (bool)drMaster["Duyet"];            
            //bool isDeleteMaster = drMaster.RowState == DataRowState.Deleted ? true : false;            
            //// khi thay đổi Duyệt.
            //if (drMaster.RowState == DataRowState.Modified
            //    && ((bool)drMaster["Duyet"] != (bool)drMaster["Duyet",DataRowVersion.Original]))
            //{
            //    Excute_DuyetOrHuyDuyetPhieu();                        
            //} 
        }

        public void ExecuteBefore()
        {
            if (data.CurMasterIndex < 0)
                return;
            drMaster = data.DsData.Tables[0].Rows[data.CurMasterIndex];
            using (dv_DTDNKG = GetDetailView())
            {
                // Kiểm tra dữ liệu khi Duyệt or Bỏ duyệt
                if (!ValidateDuyet())
                    return;
                //// Kiểm tra nếu trình duyệt thì phải chọn MALOP
                if (!Validate())
                    return;

                //// khi delete or add new 1 row nào đó trong DTDNKH thì update lại sỉ số(SiSo) cho master
                if (drMaster.RowState != DataRowState.Deleted)
                    SetSiso();
                // Xử lý khi duyệt phiếu
                if (drMaster.RowState == DataRowState.Modified
                    && drMaster[colTinhTrang, DataRowVersion.Original].ToString() != drMaster[colTinhTrang].ToString()
                    && drMaster[colTinhTrang].ToString() == TinhTrang.Duyet)
                {
                    Excute_DuyetOrHuyDuyetPhieu();
                }
            }
        }

        private bool ValidateDuyet()
        {
            if (!Boolean.Parse(Config.GetValue("Admin").ToString()) && !Boolean.Parse(data.DrTable["sApprove"].ToString()))
            {
                info.Result = false;
                XtraMessageBox.Show("Không có quyền thực hiện chức năng", Config.GetValue("PackageName").ToString());
                return info.Result;
            }            
            DataRowState currentRowState = drMaster.RowState;
            string TinhTrang_Ori = "";
            string TinhTrang_Curr = "";
            if(currentRowState == DataRowState.Modified || currentRowState == DataRowState.Deleted)
                TinhTrang_Ori = drMaster[colTinhTrang, DataRowVersion.Original].ToString();
            if(currentRowState == DataRowState.Added || currentRowState == DataRowState.Modified)
                TinhTrang_Curr = drMaster[colTinhTrang].ToString();

            // Thêm
            if(currentRowState == DataRowState.Added)
            {
                if(TinhTrang_Curr == TinhTrang.Duyet)
                {
                    info.Result = false;
                    XtraMessageBox.Show("Thao tác không hợp lệ\n" + TinhTrang.ChuaDuyet + " --> " + TinhTrang.TrinhDuyet,
                        Config.GetValue("PackageName").ToString());
                    return info.Result;
                }
            }
            // Xóa
            if(currentRowState == DataRowState.Deleted)
            {
                // đã trình duyệt/duyệt thì không làm gì hết
                if (TinhTrang_Ori != TinhTrang.ChuaDuyet)
                {
                    info.Result = false;
                    XtraMessageBox.Show("Không thể sửa/xóa khi phiếu đã được trình duyệt/duyệt!",
                        Config.GetValue("PackageName").ToString());
                    return info.Result;
                }
            }
            // Sửa
            if(currentRowState == DataRowState.Modified)
            {
                // đã duyệt thì không làm gì hết
                if(TinhTrang_Ori == TinhTrang.Duyet )
                {
                    info.Result = false;
                    XtraMessageBox.Show("Không thể sửa/xóa khi phiếu đã được duyệt!",
                        Config.GetValue("PackageName").ToString());
                    return info.Result;
                }
                // đang là tình trạng trình duyệt
                if(TinhTrang_Ori == TinhTrang.TrinhDuyet)                        
                {
                    
                    // Không thể sửa/xóa khi phiếu đã trình duyệt!
                    if(isChangeMasterWithOutTinhTrang())                        
                    {
                        info.Result = false;
                        XtraMessageBox.Show("Không thể sửa/xóa khi phiếu đã trình duyệt!",
                            Config.GetValue("PackageName").ToString());
                        return info.Result;
                    }
                }
                // đang là tình trạng chưa duyệt
                if(TinhTrang_Ori == TinhTrang.ChuaDuyet)
                {
                    if(TinhTrang_Ori != TinhTrang_Curr && TinhTrang_Curr == TinhTrang.Duyet)                        
                    {
                        info.Result = false;
                        XtraMessageBox.Show("Thao tác không hợp lệ\n" + TinhTrang.ChuaDuyet + " --> " + TinhTrang.TrinhDuyet,
                            Config.GetValue("PackageName").ToString());
                        return info.Result;
                    }
                }
            }            
            // Unchanged
            if (currentRowState == DataRowState.Unchanged)
            {
                string TinhTrangCur = drMaster[colTinhTrang].ToString();
                if (TinhTrangCur != TinhTrang.ChuaDuyet && isChangesDetail())
                {
                    info.Result = false;
                    XtraMessageBox.Show("Không thể sửa/xóa khi phiếu đã trình duyệt/duyệt!",
                                Config.GetValue("PackageName").ToString());
                    return false;
                }
            }                           
            return info.Result;
        }
        private bool ValidateMTDK(string MaLop)
        {
            string sql = "";
            foreach (DataRowView drv in dv_DTDNKG)            
                sql += string.Format(@"(HVTVID = {0} And MaLop = '{1}' And Duyet = 1) Or", (int)drv.Row["HVTVID"], MaLop);            
            if (sql != "")
            {
                sql = sql.Substring(0, sql.Length - 2);
                sql = string.Format("If Exists (Select HVID From MTDK Where {0}) Select 1 Else Select 0", sql);
                if ((int)db.GetValue(sql) == 1)
                {
                    XtraMessageBox.Show("Không thể bỏ duyệt phiếu đề nghị khai giảng ! Vì danh sách học viên đã được duyệt học lớp " + MaLop,
                                       Config.GetValue("PackageName").ToString(),
                                       MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            return true;
        }
        private bool ValidateDetail()
        {
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
            {
                string TinhTrang_Ori = drMaster[colTinhTrang,DataRowVersion.Original].ToString();
                if (TinhTrang_Ori != TinhTrang.ChuaDuyet && isChangesDetail() != null)
                {
                    XtraMessageBox.Show("Không thể sửa/xóa khi phiếu đã trình duyệt/duyệt!",
                                Config.GetValue("PackageName").ToString());
                    return false;
                }
            }           
            return true;            
        }
        private bool isChangeMasterWithOutTinhTrang()
        {
            string colName = "";
            for (int i = 0; i < data.DsData.Tables[0].Columns.Count; i++)
            {
                colName = data.DsData.Tables[0].Columns[i].ColumnName;
                if (colName != colTinhTrang
                    && (drMaster[colName, DataRowVersion.Original].ToString() != drMaster[colName].ToString()))
                    return true;
            }
            return false;
        }
        private bool Validate()
        {
            if (drMaster.RowState == DataRowState.Modified
                && drMaster[colTinhTrang, DataRowVersion.Original].ToString() != drMaster[colTinhTrang].ToString()
                && drMaster[colTinhTrang].ToString() == TinhTrang.TrinhDuyet
                && drMaster["MaLop"] == DBNull.Value)
            {
                XtraMessageBox.Show("Vui lòng chọn lớp học.", Config.GetValue("PackageName").ToString());
                info.Result = false;                
            }
            return info.Result;
        }
        private bool isChangesDetail()
        {
            using (DataView dv = GetDetailView())
            {
                foreach (DataRowView drv in dv)
                {
                    if (drv.Row.RowState == DataRowState.Added
                        || drv.Row.RowState == DataRowState.Modified
                            || drv.Row.RowState == DataRowState.Deleted)
                        return true;
                }
            }
            return false;
            //return data.DsData.Tables[1].GetChanges(DataRowState.Deleted | DataRowState.Modified | DataRowState.Added);
        }
        private DataView GetDetailView()
        {
            string MTKGID = drMaster.RowState != DataRowState.Deleted ? drMaster["MTKGID"].ToString() : drMaster["MTKGID", DataRowVersion.Original].ToString();            
            DataView dvDTDNKG = new DataView(data.DsData.Tables[1]);
            dvDTDNKG.RowStateFilter = DataViewRowState.CurrentRows | DataViewRowState.Deleted;
            dvDTDNKG.RowFilter = "MTKGID = '" + MTKGID + "'";
            return dvDTDNKG;
        }
        private string GetSQL_DuyetOrHuyDuyetPhieu(string Mode, string MaLop, string DTDKID, string HVTVID)
        {
            //string sql = "";
            //string temp = "";
            //temp = Mode == "Duyệt" ? "'{1}'" : "NULL";
            //sql += string.Format(@" UPDATE DTDKLop SET MaLop = " + temp + " WHERE ID = '{0}' ; ", DTDKID, MaLop);
            //temp = Mode == "Duyệt" ? "'NEW'" : "'DELETE'";

            string sql = string.Format(@"
                    
                    SET @MALOP_NEW = '{0}'
                    SET @DTDKLOPID = '{2}'
                    SET @HVTVID = {1} 
                    SET @MTDKID_NEW = NEWID()
                        
                    UPDATE DTDKLop SET MaLop = @MALOP_NEW WHERE ID = '{2}'
                    
                    SELECT  @MAHVTV = HV.MAHV,@TENHV = HV.TENHV,@NGAYDK = DK.NGAYDK,@MANHOMLOP = DK.MACD
                            ,@MACNDK = HV.MACN,@NGAYSINH = HV.NGAYSINH
                            ,@MOTATINHTRANGHV = N'Chuyển tình trạng: ' + TT.TINHTRANG + N' sang Đã xếp lớp '
                    FROM    DMHVTV HV INNER JOIN DTDKLOP DK ON DK.HVTVID = HV.HVTVID INNER JOIN DMTINHTRANG TT ON TT.ID = HV.TINHTRANG
                    WHERE   DK.ID = '{2}'

                    EXEC SP_InsertMTDK '',@HVTVID,@DTDKLOPID,'NEW',@MAHVTV,@TENHV,@NGAYDK,@MANHOMLOP,@MACNDK,@MALOP_NEW,@NGAYSINH,0,@MTDKID_NEW
                    
                    UPDATE MTDK SET DUYET = 1, NguoiDuyet = '{4}' WHERE HVID = @MTDKID_NEW
                    UPDATE DMHVTV SET TINHTRANG = {3} WHERE HVTVID = @HVTVID

                    INSERT INTO DTTINHTRANG(HVID,NGAY,TINHTRANGID,MANV,MOTA)
                    VALUES(@HVTVID,GETDATE(),{3},'{4}',@MOTATINHTRANGHV)"
                , MaLop, HVTVID, DTDKID, TinhTrangHV.DaXepLop, drMaster["NguoiDuyet"]);
            return sql;
        }
        private void Excute_DuyetOrHuyDuyetPhieu()
        {
            string MaLop = drMaster["MaLop"].ToString();
            drMaster["NguoiDuyet"] = Config.GetValue("UserName");
            string sql = @"
                    DECLARE @MTDKID_NEW  NVARCHAR(128)                    
                    DECLARE @DTDKLOPID  NVARCHAR(128)
                    DECLARE @HVTVID     INT
                    DECLARE @MAHVTV		NVARCHAR(128)
	                DECLARE @TENHV		NVARCHAR(128)
	                DECLARE @NGAYDK		NVARCHAR(128)
	                DECLARE @MANHOMLOP	NVARCHAR(128)	
	                DECLARE @MACNDK		NVARCHAR(128)
	                DECLARE @MALOP_NEW	NVARCHAR(128)
	                DECLARE @NGAYSINH	NVARCHAR(128)
                    DECLARE @MOTATINHTRANGHV	NVARCHAR(128)";
            int count = 0;
            foreach (DataRowView drv in dv_DTDNKG)
            {
                // Chưa duyệt --> duyệt
                if (drv.Row["DTDKID"] != DBNull.Value)
                {
                    sql += GetSQL_DuyetOrHuyDuyetPhieu("Duyệt", MaLop, drv.Row["DTDKID"].ToString(), drv.Row["HVTVID"].ToString());
                    count++;
                }
                //// Duyệt --> chưa duyệt
                //else                
                //    sql += GetSQL_DuyetOrHuyDuyetPhieu("Hủy Duyệt", MaLop, drv.Row["DTDKID"].ToString(), drv.Row["HVTVID"].ToString());                
            }
            if (count > 0)
                data.DbData.GetValue(sql);
            //cap nhat si so cua lop
            sql = @"update DMLopHoc set SiSoHV = (select count(*) from MTDK where MaLop = '{0}'
                        and IsNghiHoc = 0 and IsBL = 0) where MaLop = '{0}';
                    update DMLopHoc set SiSo = (select count(*) from MTDK where MaLop = '{0}'
                        and Duyet = 1 and IsNghiHoc = 0 and IsBL = 0) where MaLop = '{0}';";
            data.DbData.UpdateByNonQuery(string.Format(sql, drMaster["MaLop"]));
        }                
        private void SetSiso()
        {
            dv_DTDNKG.RowStateFilter = DataViewRowState.CurrentRows;
            drMaster["SiSo"] = dv_DTDNKG.Count;
        }

        public InfoCustomData Info
        {
            get { return info; }
        }
        public static class TinhTrang
        {
            public static string Duyet = "Đã duyệt";
            public static string ChuaDuyet = "Chưa duyệt";
            public static string TrinhDuyet = "Trình duyệt";
        }
        public static class TinhTrangHV
        {
            public static int DaNghi = 1;
            public static int DangBaoLuu = 2;
            public static int DangChoChuyenLop = 3;
            public static int DaXepLop = 4;
            public static int DangHocThu = 5;
            public static int DangChoXepLop = 6;
            public static int DaTuVan = 7;
            public static int MoiLienHe = 8;
        }
        #endregion
    }
}
