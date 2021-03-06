using System;
using System.Collections.Generic;
using System.Text;
using DevExpress;
using CDTDatabase;
using CDTLib;
using Plugins;
using System.Data;
using DevExpress.XtraEditors;
using System.Windows.Forms;
using SaveDangKyLop;
using DangKyLopICC2;

namespace SaveDangKyLop2
{
    public class SaveDangKyLop2 : ICData
    {
        private Database db = Database.NewDataDatabase();
        private InfoCustomData _info;
        private DataCustomData _data;
        private DataRow drMaster;
        private DateTime DateNow = DateTime.Today;
        private string MaNV = Config.GetValue("UserName").ToString();
        private string ListChanges_DTDKLopID = "";
        private int HVTVID;
        private int TinhTrangDauTien = -1;
        private HocVienInfos hvInfo;
        private QuanLyCT quanlyCT;
        //int TinhTrangDauTien;        
         
        public SaveDangKyLop2()
        {
            _info = new InfoCustomData(IDataType.Detail);
        }
        public DataCustomData Data
        {
            set { _data = value; }
        }
        public InfoCustomData Info
        {
            get { return _info; }
        }

        public void ExecuteAfter()
        {
            /////------------Cap nhat si so dang ky cua lop
            List<string> lstLop = new List<string>();   //lấy danh sách các lớp thay đổi sỉ số
            using (DataView dv = new DataView(_data.DsDataCopy.Tables[0]))
            {
                dv.RowStateFilter = DataViewRowState.Added | DataViewRowState.Deleted;
                foreach (DataRowView drv in dv)
                {
                    string malop = drv["MaLop"].ToString();
                    if (!lstLop.Contains(malop))
                        lstLop.Add(malop);
                }
            }
            if (lstLop.Count == 0)
                return;
            _data.DbData.EndMultiTrans();   //kết thúc transaction để có thể cập nhật
            string sql = @"update DMLopHoc set SiSoHV = (select count(*) from MTDK where DMLopHoc.MaLop = MTDK.MaLop
                                and IsNghiHoc = 0 and IsBL = 0) where MaLop in ({0})";
            string tmp = ""; 
            foreach (string malop in lstLop)
                tmp += string.Format("'{0}', ", malop);
            tmp = tmp.Remove(tmp.Length - 2);
            if (db.UpdateByNonQuery(string.Format(sql, tmp)))   //cập nhật vào database
            {
                //cap nhat vao giao dien
                DataTable dt = db.GetDataTable(string.Format("select MaLop, SiSoHV from DMLopHoc where MaLop in ({0})", tmp));
                foreach (DataRow dr in dt.Rows)
                {
                    DataRow[] drs = _data.DsData.Tables[1].Select("MaLop = '" + dr["MaLop"].ToString() + "'");
                    if (drs.Length > 0)
                        drs[0]["SiSoHV"] = dr["SiSoHV"];
                }
                _data.DsData.Tables[1].AcceptChanges();
            }
        }
        public void ExecuteBefore()
        {
            if (_data.CurMasterIndex < 0)
                return;
            //_info.Result = true;
            drMaster = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];
            HVTVID = GetHVTVID(drMaster);
            // get student infos
            DataTable dtHocVienInfo = GetHocVienInfos(HVTVID);
            if (dtHocVienInfo.Rows.Count == 0)
                return;
            hvInfo = new HocVienInfos();            
            hvInfo.tenHV = dtHocVienInfo.Rows[0]["TenHV"].ToString();
            hvInfo.maHV = dtHocVienInfo.Rows[0]["MaHV"].ToString();
            hvInfo.maCN = dtHocVienInfo.Rows[0]["MaCN"].ToString();            
            /*-- Kiểm tra thu học phí nhiều lần trước tiên*/
            if (drMaster.RowState == DataRowState.Deleted || drMaster.RowState == DataRowState.Modified)
            {
                bool isThuNLan = false;
                if (!KiemTraThuNLan(ref isThuNLan))
                    return;
                if (isThuNLan)
                    return;
            }
            /*-- Yếu cầu 1:
                 * KIểm tra xem các đăng ký lớp đã được duyệt hay chưa nếu duyệt rồi thì không cho phép sửa/xóa dữ liệu trong DTDKLop  --*/
            #region Thực hiện YÊU CẦU 1
            List<string> list_MTDKsWillDelete = new List<string>();
            if (!KiemTraDangKyLop(ref list_MTDKsWillDelete)) // code cũ có tham số truyền vào "ref list_MTDKsWillDelete)"
                return;
            #endregion
                        
            //using (DataView dvDTDKLop = GetDetailView())
            //{                
                /*-- Yếu cầu 1:
                 * KIểm tra xem các đăng ký lớp đã được duyệt hay chưa nếu duyệt rồi thì không cho phép sửa/xóa dữ liệu trong DTDKLop  --*/
                //#region Thực hiện YÊU CẦU 1
                //List<string> list_MTDKsWillDelete = new List<string>();
                //if (!KiemTraDangKyLop(dvDTDKLop, ref list_MTDKsWillDelete))
                //    return;
                //#endregion


                /*-- Yêu cầu 2:
                -	Ktra khi có nộp tiền, yêu cầu chọn hình thức thanh toán.
                -	Hình thức thanh toán = “TM”và Đã nộp > 0 (*) – Tham khảo function Lapphieuthu trong plugin CapNhatThuVonPhi của hội nông dân TP
                    o	Tạo phiếu thu tiền mặt - MT11: TKNo 1111 – TKCo 5113, lưu số phiếu thu vào ô “Phiếu thu” trong đăng ký học ở trên.
                    o	Tạo phiếu thu vào BLTK
                -	Hình thức thanh toán <> “TM” và Đã nộp > 0 (**) 
                    o	Lập phiếu thu ngân hàng - MT15: TKNo 1121 – TKCo 5113.
                    o	Tạo phiếu thu vào BLTK
                -	Nếu có check IsMuaBT: tạo thêm dòng chi tiết ở các phiếu thu (*) và (**) (TKCo 5111)
                -	Tạo phiếu xuất bán giáo trình (tham khảo plugin : Nếu IsMuaBT = 1. Dựa vào cấp độ lúc đăng ký của học viên và dữ liệu từ bảng vật tư nhóm lớp
                    o	Tạo phiếu xuất vật tư MT43: tham khảo xuất vật tư giáo trình ở màn hình đăng ký học viên của APEX
                    o	Tạo dữ liệu vào BLVT */
                #region Thực hiện yêu cầu 2

                if (!KiemTraBeforePhieuThus())
                    return;

                #endregion


                if (drMaster.RowState == DataRowState.Deleted)
                {
                    DeleteAllDatasBefore(list_MTDKsWillDelete);
                }

                /////------------Update final datas------------/////
                UpdateAllDatas();

            //}
        }
        private void DeleteAllDatasBefore(List<string> list_MTDKsWillDelete)
        {
            
            string sql1 = "";
            // xóa những dữ liệu có tham chiếu đến MTDK
            if (list_MTDKsWillDelete != null)
            {
                foreach (string item in list_MTDKsWillDelete)
                {
                    sql1 += string.Format(@"
                                Exec SP_DeleteAllDatasFromForeignTable 'MTDK','{0}'", item);

                }
            }
            if (sql1 != "")
                _data.DbData.UpdateByNonQuery(sql1);
        }
        private void UpdateAllDatas()
        {                                    
            // UPDATE PHIẾU THUS
            UpdatePhieuThus();            

            // CẬP NHẬT TIỀN BẢO LƯU CHO MTDK            
            UpdateBaoLuu();

            // UPDATE CÁC DÒNG DTDKLOP KHI CÓ THAY ĐỔI NHỮNG FIELDS SAU : 
            // REFMTDK OR MT31ID OR MT11ID OR MT15ID OR MT43ID
            //UpdateDTDKLop(dvDTDKLop);

            // UPDATE TÌNH TRẠNG HỌC VIÊN
            UpdateTinhTrangHV();
        }
        private void UpdateBaoLuu()
        {
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
            {
                // xóa 
                if (drMaster.RowState == DataRowState.Deleted)
                {
                    object old_refMTDKBL = drMaster["refMTDKBL", DataRowVersion.Original];
                    if (old_refMTDKBL != DBNull.Value)
                    {
                        decimal TienBLConLai = TienBL(old_refMTDKBL.ToString()) + (decimal)drMaster["TienBL", DataRowVersion.Original];
                        UpdateTienBL(old_refMTDKBL.ToString(), TienBLConLai);
                    }
                }
                // sửa
                if (drMaster.RowState == DataRowState.Modified)
                {
                    // khi tiền bảo lưu được lấy từ một MTDK khác.
                    if (drMaster["refMTDKBL", DataRowVersion.Original].ToString() != drMaster["refMTDKBL"].ToString())
                    {
                        // cập nhật tiền bl còn lại cho mtdk cũ
                        object old_refMTDKBL = drMaster["refMTDKBL", DataRowVersion.Original];
                        if (old_refMTDKBL != DBNull.Value)
                        {
                            decimal old_tienBL = (decimal)drMaster["TienBL", DataRowVersion.Original];
                            decimal ori_tienBL = TienBL(old_refMTDKBL.ToString());
                            decimal TienBLConLai = ori_tienBL + old_tienBL;
                            UpdateTienBL(old_refMTDKBL.ToString(), TienBLConLai);
                        }
                        // cập nhật tiền bl còn lại cho mtdk mới
                        object new_refMTDKBL = drMaster["refMTDKBL"];
                        if (new_refMTDKBL != DBNull.Value)
                        {                            
                            decimal cur_tienBL = (decimal)drMaster["TienBL"]; // tiền bảo lưu hiện tại.
                            decimal ori_tienBL = TienBL(new_refMTDKBL.ToString()); // tiền bảo lưu gốc còn lại trong MTDK
                            decimal TienBLConLai = ori_tienBL - cur_tienBL;
                            UpdateTienBL(new_refMTDKBL.ToString(), TienBLConLai);
                        }
                    }
                    // vẫn không thay đổi MTDK (nơi lấy tiền bảo lưu)
                    else
                    {
                        // tiền bảo lưu thay đổi
                        if ((decimal)drMaster["TienBL", DataRowVersion.Original] != (decimal)drMaster["TienBL"])
                        {                            
                            object refMTDKBL = drMaster["refMTDKBL"];
                            if (refMTDKBL != DBNull.Value)
                            {
                                decimal old_tienBL = (decimal)drMaster["TienBL", DataRowVersion.Original]; // tiền bảo lưu trước khi sửa
                                decimal cur_tienBL = (decimal)drMaster["TienBL"]; // tiền bảo lưu hiện tại.
                                decimal ori_tienBL = TienBL(refMTDKBL.ToString()); // tiền bảo lưu gốc còn lại trong MTDK
                                decimal TienBLConLai = ori_tienBL + old_tienBL - cur_tienBL;
                                UpdateTienBL(refMTDKBL.ToString(), TienBLConLai);
                            }
                        }
                    }
                }
            }
            // thêm mới
            if (drMaster.RowState == DataRowState.Added)
            {
                if ((decimal)drMaster["TienBL"] > 0)
                {
                    object refMTDKBL = drMaster["refMTDKBL"];
                    if (refMTDKBL != DBNull.Value)
                    {
                        decimal TienBLConLai = TienBL(refMTDKBL.ToString()) - (decimal)drMaster["TienBL"];
                        UpdateTienBL(refMTDKBL.ToString(), TienBLConLai);
                    }
                }
            }
        }
        private void UpdateTienBL(string refMTDKBL,decimal TienBLConLai)
        {
            string sql = string.Format(@"Update MTDK Set BLSoTien = {1} Where HVID = '{0}'", refMTDKBL, TienBLConLai);
            _data.DbData.UpdateByNonQuery(sql);
        }
        private decimal TienBL(string MTDKID)
        {
            // lấy thông tin bảo lưu.
            string sql = string.Format("Select Top 1 BLSoTien, HVID From MTDK Where HVID = '{0}' Order By NgayBL Desc", MTDKID);
            DataTable dtInfo = db.GetDataTable(sql);
            if (dtInfo.Rows.Count > 0)
            {                
                return (decimal)dtInfo.Rows[0]["BLSoTien"];
            }
            else
                return 0;
        }
        private void UpdateTinhTrangHV()
        {                                    
            if (drMaster.RowState == DataRowState.Deleted)
            {
                Xuly_DeleteTinhTrang();
            }
            if (drMaster.RowState == DataRowState.Added)
            {
                Xuly_AddTinhTrang();
            }
            if (drMaster.RowState == DataRowState.Modified)
            {
                // nếu có thay đổi học viên khác thì mới cập nhật lại tình trạng học viên.
                if ((int)drMaster["HVTVID", DataRowVersion.Original] != (int)drMaster["HVTVID"])
                {
                    // cập nhật lại tình trạng cho học viên cũ
                    Xuly_DeleteTinhTrang();
                    // cập nhật tình trạng cho học viên mới.
                    Xuly_AddTinhTrang();
                }
            }            
        }
        private void Xuly_DeleteTinhTrang()
        {
            int hvtvid = (int)drMaster["HVTVID", DataRowVersion.Original];
            int TinhTrang_Ori = GetCurrentTinhTrangOfHVTV(hvtvid);
            int TinhTrang_Cur = -1;  
            // cách làm này vẫn chưa tối ưu , là cách làm tạm thời vẩn còn nhiều lổ hỏng, cần xem lại.
            string sql = string.Format("select top 1 tinhtrangid from dttinhtrang where hvid = {0} and tinhtrangid <> {1} order by id desc", hvtvid, TinhTrang_Ori);
            object o = _data.DbData.GetValue(sql);
            if (o == null)
                TinhTrang_Cur = TinhTrangs.MoiLienHe;
            else
                TinhTrang_Cur = (int)o;
            XuLy_UpdateTinhTrangHV(TinhTrang_Ori, TinhTrang_Cur, hvtvid);
        }
        private void Xuly_AddTinhTrang()
        {
            int hvtvid = (int)drMaster["HVTVID"];
            int TinhTrang_Ori = GetCurrentTinhTrangOfHVTV(hvtvid);
            int TinhTrang_Cur = TinhTrangs.DangChoXepLop;
            XuLy_UpdateTinhTrangHV(TinhTrang_Ori, TinhTrang_Cur, hvtvid);
        }
        private int GetCurrentTinhTrangOfHVTV(int hvtvid)
        {
            string sql = "Select TinhTrang From DMHVTV Where HVTVID = " + hvtvid;
            return (int)_data.DbData.GetValue(sql);
        }
        private void XuLy_UpdateTinhTrangHV(int TinhTrang_Ori, int TinhTrang_Cur,int hvtvid)
        {
            if (TinhTrang_Cur == -1 || TinhTrang_Ori == -1)
                return;
            string MoTa = "";
            string sql = string.Format(@"
                        Select N'Chuyển tình trạng: ' + old.TinhTrang + ' sang ' + new.TinhTrang From DMTinhTrang old,DMTinhTrang new Where old.ID = {0} and new.ID = {1}"
                                   , TinhTrang_Ori, TinhTrang_Cur);
            
            MoTa = _data.DbData.GetValue(sql).ToString();
            sql = string.Format(@"
                        Declare @MoTa nvarchar(128)                        
                        Insert Into DTTinhTrang(HVID,Ngay,TinhTrangID,MaNV,MoTa)
                        Values({0},'{1}',{3},'{4}',N'{5}')
                            
                        Update DMHVTV Set TinhTrang  = {3} Where HVTVID = {0}"
                , hvtvid, DateNow, TinhTrang_Ori, TinhTrang_Cur, MaNV, MoTa);

            if (!_data.DbData.UpdateByNonQuery(sql))
                _info.Result = false;         
        }
        private void UpdatePhieuThus()
        {
            //foreach (DataRowView drv in dvDTDKLop)
            //{
                quanlyCT = new QuanLyCT(hvInfo.maHV, hvInfo.tenHV, _data.DbData);
                // thêm                
                if (drMaster.RowState == DataRowState.Added)
                {
                    // nếu có nộp tiền thì mới tạo phiếu thus
                    if (drMaster["HTTT"] != DBNull.Value && (decimal)drMaster["TDaNop"] > 0)
                    {
                        Insert_MT11_MT15_MT43_BLTK_BLVT(true);
                        // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                        // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                        //GetListChanges_DTDKLopID(drv);
                    }                                        
                }
                // Sửa..
                // Ở đây không sử dụng table.Getchanges(state) để lấy ra table chứa những dòng modified vì 
                // không phải bất cứ sự thay đổi nào trên những dòng đó đều lấy ra modified
                // mà chỉ lấy khi 1 số fields bên dứoi thay đổi.
                if (drMaster.RowState == DataRowState.Modified)
                {
                    if (drMaster["NgayTN"].ToString() != drMaster["NgayTN", DataRowVersion.Original].ToString()
                        || drMaster["HTTT"].ToString() != drMaster["HTTT", DataRowVersion.Original].ToString()
                        //|| drMaster["TienGT"].ToString() != drMaster["TienGT", DataRowVersion.Original].ToString()
                        //|| drMaster["TienHP"].ToString() != drMaster["TienHP", DataRowVersion.Original].ToString()
                        || drMaster["TDaNop"].ToString() != drMaster["TDaNop", DataRowVersion.Original].ToString()
                        || drMaster["HVTVID"].ToString() != drMaster["HVTVID", DataRowVersion.Original].ToString())
                    {
                        Update_MT11_MT15_MT43_BLTK_BLVT();
                        // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                        // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                        //GetListChanges_DTDKLopID(drv);
                    }
                }
                // xóa
                if (drMaster.RowState == DataRowState.Deleted)
                {
                    quanlyCT.Delete_MT11_MT15_MT43_BLTK_BLVT(drMaster);
                    // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                    // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                    //GetListChanges_DTDKLopID(drv);
                }
            //}
            //decimal a = DangKyLopICC.DangKyLopICC.f_TongTienBL;            
        }

        //Công thêm để kiểm tra thu phí nhiều lần -> không cho sửa xóa nhưng vẫn cho cập nhật số tiền thực thu (phần mềm cập nhật sau khi thu nhiều lần)
        private bool KiemTraThuNLan(ref bool isThuNLan)
        {
            string msgThuNhieuLan = "";
            object thuNLan = _data.DbData.GetValue(string.Format("select sum(SoTien) from CTThuTien where DTDKLop = '{0}'", drMaster["ID", DataRowVersion.Original]));
            if (thuNLan != null && thuNLan != DBNull.Value && Convert.ToDecimal(thuNLan) > 0)
            {
                if (drMaster.RowState == DataRowState.Deleted)  //nếu đã thu nhiều lần thì không được xóa
                    msgThuNhieuLan += GetMessageDetail(drMaster);
                else
                {
                    if (drMaster["TDaNop", DataRowVersion.Current] != drMaster["TDaNop", DataRowVersion.Original])
                    {
                        //nếu sửa TDaNop thì kiểm tra TDaNop = tổng thu trong bltk
                        object tThu = _data.DbData.GetValue(string.Format("select sum(PsNo) from BLTK where RefDTDKLop = '{0}'", drMaster["ID", DataRowVersion.Original]));
                        if ((drMaster["TDaNop"] != DBNull.Value && tThu == null)
                            || (Convert.ToDecimal(drMaster["TDaNop"]) != Convert.ToDecimal(tThu)))
                            msgThuNhieuLan += GetMessageDetail(drMaster);
                        else
                            isThuNLan = true;
                    }
                }
            }
            if (msgThuNhieuLan != "")
            {
                XtraMessageBox.Show("Đã thu học phí nhiều lần, không cho phép sửa/xóa dữ liệu !\nVui lòng kiểm tra dữ liệu tại những dòng sau:\n" + msgThuNhieuLan, Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return false;
            }
            return true;
        }

        private bool KiemTraChungTu()
        {
            string Message = "";
            //foreach (DataRowView drv in dv)
            //{
            if (drMaster.RowState == DataRowState.Deleted ||
                (drMaster.RowState == DataRowState.Modified && (
                       drMaster["NgayTN"].ToString() != drMaster["NgayTN", DataRowVersion.Original].ToString()
                    || drMaster["HTTT"].ToString() != drMaster["HTTT", DataRowVersion.Original].ToString()
                    || drMaster["MaCD"].ToString() != drMaster["MaCD", DataRowVersion.Original].ToString()
                    || drMaster["TienHP"].ToString() != drMaster["TienHP", DataRowVersion.Original].ToString()
                    || drMaster["TDaNop"].ToString() != drMaster["TDaNop", DataRowVersion.Original].ToString()
                    || drMaster["refMTDKBL"].ToString() != drMaster["refMTDKBL", DataRowVersion.Original].ToString()
                    || drMaster["HVTVID"].ToString() != drMaster["HVTVID", DataRowVersion.Original].ToString()
                   )
                ))
            {
                string MT11 = drMaster["MT11ID", DataRowVersion.Original] != DBNull.Value ? drMaster["MT11ID", DataRowVersion.Original].ToString() : "";
                string MT15 = drMaster["MT15ID", DataRowVersion.Original] != DBNull.Value ? drMaster["MT15ID", DataRowVersion.Original].ToString() : "";
                string MT31 = drMaster["MT31ID", DataRowVersion.Original] != DBNull.Value ? drMaster["MT31ID", DataRowVersion.Original].ToString() : "";
                string MT43 = drMaster["MT43ID", DataRowVersion.Original] != DBNull.Value ? drMaster["MT43ID", DataRowVersion.Original].ToString() : "";
                
                string sql = string.Format(@"
                DECLARE @MT11ID NVARCHAR(128)
                DECLARE @MT31ID NVARCHAR(128)
                DECLARE @MT15ID NVARCHAR(128)
                DECLARE @MT43ID NVARCHAR(128)
                SET @MT11ID = '{0}'
                SET @MT15ID = '{1}'
                SET @MT31ID = '{2}'
                SET @MT43ID = '{3}'
                IF @MT11ID = ''
                    SET @MT11ID = NULL
                IF @MT15ID = ''
                    SET @MT15ID = NULL
                IF @MT31ID = ''
                    SET @MT31ID = NULL
                IF @MT43ID = ''
                    SET @MT43ID = NULL
                IF EXISTS
                (	                
                    SELECT MT11ID FROM MT11 WHERE MT11ID = @MT11ID AND DUYET = 1
                    UNION ALL
                    SELECT MT15ID FROM MT15 WHERE MT15ID = @MT15ID AND DUYET = 1
                    UNION ALL
                    SELECT MT31ID FROM MT31 WHERE MT31ID = @MT31ID AND DUYET = 1
                    UNION ALL
                    SELECT MT43ID FROM MT43 WHERE MT43ID = @MT43ID AND DUYET = 1
                ) SELECT 1 ELSE SELECT 0"
                    , MT11, MT15, MT31, MT43);
                if ((int)_data.DbData.GetValue(sql) == 1)
                    Message += GetMessageDetail(drMaster);
            }
            //}
            // phiếu MT11 or MT15 or MT31 đã duyệt thì không được xóa/sửa
            if (Message != "")
            {
                XtraMessageBox.Show("Phiếu thu học phí đã được duyệt, không cho phép sửa/xóa dữ liệu !\nVui lòng kiểm tra dữ liệu tại những dòng sau:\n" + Message, Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return false;
            }
            return true;
        }
        private void Update_MT11_MT15_MT43_BLTK_BLVT()
        {
            // Xóa dữ liệu cũ
            quanlyCT.Delete_MT11_MT15_MT43_BLTK_BLVT(drMaster);

            // Insert mới các phiếu
            if (drMaster["HTTT"] != DBNull.Value && (decimal)drMaster["TDaNop"] > 0)
            {
                Insert_MT11_MT15_MT43_BLTK_BLVT(true);
            }
        }
        public void Delete_MT11_MT15_MT43_BLTK_BLVT()
        {                        
            string sql = string.Format(@"

                    DECLARE @MT31ID NVARCHAR(128)
                    DECLARE @MT11ID NVARCHAR(128)
                    DECLARE @MT15ID NVARCHAR(128)
                    DECLARE @MT43ID NVARCHAR(128)                    
                    
                    SET @MT31ID = '{0}'
                    SET @MT11ID = '{1}'
                    SET @MT15ID = '{2}'
                    SET @MT43ID = '{3}'                    

                    IF @MT31ID = ''
                        SET @MT31ID = NULL
                    IF @MT11ID = ''
                        SET @MT11ID = NULL
                    IF @MT15ID = ''
                        SET @MT15ID = NULL
                    IF @MT43ID = ''
                        SET @MT43ID = NULL
                        
                    -- XÓA DỮ LIỆU CŨ
                    DELETE FROM DT31 WHERE MT31ID = @MT31ID
                    DELETE FROM DT11 WHERE MT11ID = @MT11ID
                    DELETE FROM DT15 WHERE MT15ID = @MT15ID
                    DELETE FROM DT43 WHERE MT43ID = @MT43ID
                    DELETE FROM BLTK WHERE MTID = @MT11ID
                    DELETE FROM BLTK WHERE MTID = @MT15ID
                    DELETE FROM BLVT WHERE MTID = @MT43ID

                    -- MT11                    
						DELETE FROM MT11 WHERE MT11ID = @MT11ID
                    -- MT15
						DELETE FROM MT15 WHERE MT15ID = @MT15ID                    
                    -- MT31                    
						DELETE FROM MT31 WHERE MT31ID = @MT31ID                    
                    -- MT43                    
						DELETE FROM MT43 WHERE MT43ID = @MT43ID"
                    , drMaster["MT31ID", DataRowVersion.Original]
                    , drMaster["MT11ID", DataRowVersion.Original]
                    , drMaster["MT15ID", DataRowVersion.Original]
                    , drMaster["MT43ID", DataRowVersion.Original]);
            if (drMaster.RowState != DataRowState.Deleted)
            {
                drMaster["MT11ID"] = DBNull.Value;
                drMaster["MT15ID"] = DBNull.Value;
                drMaster["MT31ID"] = DBNull.Value;
                drMaster["MT43ID"] = DBNull.Value;
            }
            // thực thi
            _data.DbData.UpdateByNonQuery(sql);
        }
        public void Delete_MT11_MT15_MT43_BLTK_BLVT_Copy()
        {
            string sql = string.Format(@"

                    DECLARE @MT31ID NVARCHAR(128)
                    DECLARE @MT11ID NVARCHAR(128)
                    DECLARE @MT15ID NVARCHAR(128)
                    DECLARE @MT43ID NVARCHAR(128)
                    DECLARE @DTDKLOPID NVARCHAR(128)
                    
                    SET @MT31ID = '{0}'
                    SET @MT11ID = '{1}'
                    SET @MT15ID = '{2}'
                    SET @MT43ID = '{3}'
                    SET @DTDKLOPID = '{4}'

                    IF @MT31ID = ''
                        SET @MT31ID = NULL
                    IF @MT11ID = ''
                        SET @MT11ID = NULL
                    IF @MT15ID = ''
                        SET @MT15ID = NULL
                    IF @MT43ID = ''
                        SET @MT43ID = NULL
                        
                    -- XÓA DỮ LIỆU CŨ
                    DELETE FROM DT31 WHERE REFDTDKLOP = @DTDKLOPID
                    DELETE FROM DT11 WHERE REFDTDKLOP = @DTDKLOPID
                    DELETE FROM DT15 WHERE REFDTDKLOP = @DTDKLOPID
                    DELETE FROM DT43 WHERE REFDTDKLOP = @DTDKLOPID
                    DELETE FROM BLTK WHERE REFDTDKLOP = @DTDKLOPID
                    DELETE FROM BLVT WHERE REFDTDKLOP = @DTDKLOPID

                    -- NẾU MT11 KHÔNG CÒN DT11 NỮA THÌ XÓA LUÔN MT11 ĐÓ, 
                    -- NGƯỢC LẠI THÌ UPDATE LẠI THÔNG TIN CHO MT11 SAU KHI BÊN TRÊN ĐÃ XÓA DT11 CỦA MT11 NÀY.
                    -- ĐỐI VỚI MT31,MT15,MT43,BLTK,BLVT THỰC HIỆN TƯƠNG TỰ.
                    
                    -- MT11
                    IF NOT EXISTS (SELECT TOP 1 DT11ID FROM MT11 M INNER JOIN DT11 D ON D.MT11ID = M.MT11ID WHERE M.MT11ID = @MT11ID AND DUYET = 0)				    
						DELETE FROM MT11 WHERE MT11ID = @MT11ID
                    ELSE
                        UPDATE MT11 SET TTIEN = (SELECT ISNULL(SUM(PS),0) FROM DT11 WHERE MT11ID = @MT11ID) WHERE MT11ID = @MT11ID
                    -- MT15    
                    IF NOT EXISTS (SELECT TOP 1 DT15ID FROM MT15 M INNER JOIN DT15 D ON D.MT15ID = M.MT15ID WHERE M.MT15ID = @MT15ID AND DUYET = 0)                        
						DELETE FROM MT15 WHERE MT15ID = @MT15ID
                    ELSE
                        UPDATE MT15 SET TTIEN = (SELECT ISNULL(SUM(PS),0) FROM DT15 WHERE MT15ID = @MT15ID) WHERE MT15ID = @MT15ID

                    -- MT31
                    IF NOT EXISTS (SELECT TOP 1 DT31ID FROM MT31 M INNER JOIN DT31 D ON D.MT31ID = M.MT31ID WHERE M.MT31ID = @MT31ID AND DUYET = 0)                       
						DELETE FROM MT31 WHERE MT31ID = @MT31ID
                    ELSE
                        UPDATE MT31 SET TTIEN = (SELECT ISNULL(SUM(PS),0) FROM DT31 WHERE MT31ID = @MT31ID) WHERE MT31ID = @MT31ID
                    
                    -- MT43
                    IF NOT EXISTS (SELECT TOP 1 DT43ID FROM MT43 M INNER JOIN DT43 D ON D.MT43ID = M.MT43ID WHERE M.MT43ID = @MT43ID)                       
						DELETE FROM MT43 WHERE MT43ID = @MT43ID					
                    ELSE
                        UPDATE MT43 SET TTIEN = (SELECT ISNULL(SUM(PS),0) FROM DT43 WHERE MT43ID = @MT43ID) WHERE MT43ID = @MT43ID"
                    , drMaster["MT31ID", DataRowVersion.Original]
                    , drMaster["MT11ID", DataRowVersion.Original]
                    , drMaster["MT15ID", DataRowVersion.Original]
                    , drMaster["MT43ID", DataRowVersion.Original]
                    , drMaster["ID", DataRowVersion.Original]);
            if (drMaster.RowState != DataRowState.Deleted)
            {
                drMaster["MT11ID"] = DBNull.Value;
                drMaster["MT15ID"] = DBNull.Value;
                drMaster["MT31ID"] = DBNull.Value;
                drMaster["MT43ID"] = DBNull.Value;
            }
            // thực thi
            _data.DbData.UpdateByNonQuery(sql);
        }
        private void Insert_MT11_MT15_MT43_BLTK_BLVT(bool isInsertMT43)
        {
            if (drMaster["HTTT"] != DBNull.Value && (decimal)drMaster["TDaNop"] > 0)
            {
                string sql = "";
                decimal TDaNop = (decimal)drMaster["TDaNop"];
                //QuanLyCT ct = new QuanLyCT(hvInfo.maHV, hvInfo.tenHV, _data.DbData);

                // không phải là dịch vụ liên kết
                if (hvInfo.maCN != "DVLK")
                {
                    // Thanh toán bằng tiền mặt
                    if (drMaster["HTTT"].ToString() == "TM" && TDaNop > 0)
                    {
                        sql += quanlyCT.GetStringInsertMT11_BLTK(drMaster, isInsertMT43);
                    }
                    // Không phải thanh toán bằng tiền mặt
                    else
                    {
                        sql += quanlyCT.GetStringInsertMT15_BLTK(drMaster, isInsertMT43);
                    }
                }
                // dịch vụ liên kết
                else
                    sql += quanlyCT.GetStringInsertMT31_BLTK(drMaster, isInsertMT43);
                // thực thi
                _data.DbData.UpdateByNonQuery(sql);
            }
        }
        private DataTable GetHocVienInfos(int HVTVID)
        {
            string sql = "Select MaHV,MaCN,TenHV from DMHVTV Where HVTVID = " + HVTVID;
            return _data.DbData.GetDataTable(sql);            
        }
        private int GetHVTVID(DataRow dr)
        {
            if (dr.RowState == DataRowState.Deleted)
                return (int)dr["HVTVID", DataRowVersion.Original];
            return (int)dr["HVTVID"];
        }
        //-nếu sửa: chỉ cho sửa khi chưa duyệt MTDK
        //-nếu xóa: chỉ cho xóa khi chưa duyệt MTDK
        //  + dò các khóa ngoại liên quan đến MTDK để xóa trước
        //  + dò trong đề nghị khai giảng nếu có thì xóa trước
        private bool KiemTraDangKyLop(ref List<string> list_MTDKsWillDelete) // code cũ có tham số này "DataView dvDTDKLop"
        {
            string Mess = "";
            string mess2 = "";
            string mess3 = "";
            string DTDKLopIDs = "";
            if (drMaster.RowState == DataRowState.Deleted || drMaster.RowState == DataRowState.Modified)
            {
                //không dùng refMTDK đối với đăng ký theo lớp
                //if (drMaster["refMTDK", DataRowVersion.Original] != DBNull.Value)
                //{
                    string DTDKLopID = drMaster["ID", DataRowVersion.Original].ToString();
                    string MaLop = drMaster["MaLop", DataRowVersion.Original].ToString();
                    string sql = string.Format(@"Select HVID, Duyet From MTDK Where refDTDKLop = '{0}'", DTDKLopID);
                    DataTable dt = _data.DbData.GetDataTable(sql);
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        // MTDK đã duyệt                            
                        if ((bool)dt.Rows[0]["Duyet"])
                            Mess += GetMessageDetail(drMaster);
                        // chưa duyệt và trạng thái là delete thì mới đưa vào danh sách đợi delete MTDKs
                        else if (drMaster.RowState == DataRowState.Deleted)
                            list_MTDKsWillDelete.Add(dt.Rows[0]["HVID"].ToString());
                    }
                //}
                if (drMaster.RowState == DataRowState.Deleted)
                {
                    // kiểm tra DTDKLop đã có trong đề nghị khai giảng không
                    // nếu DNKG đã duyệt/trình duyệt thì thông báo không cho xóa
                    // nếu DNKG chưa duyệt thì thông báo lựa chọn có tiếp tục xóa hay không.
                    sql = string.Format(@"
                    If Exists (Select d.DTKGID From DTDNKG d inner join mtdnkg m on m.mtkgid = d.mtkgid Where DTDKID = '{0}' and m.TinhTrang <> N'Chưa duyệt')
                        Select 1
                    Else Select 0", drMaster["ID", DataRowVersion.Original]);
                    if ((int)_data.DbData.GetValue(sql) == 1)
                        mess2 += GetMessageDetail(drMaster);

                    sql = string.Format(@"
                    If Exists (Select d.DTKGID From DTDNKG d inner join mtdnkg m on m.mtkgid = d.mtkgid Where DTDKID = '{0}' and m.TinhTrang = N'Chưa duyệt')
                        Select 1
                    Else Select 0", drMaster["ID", DataRowVersion.Original]);
                    if ((int)_data.DbData.GetValue(sql) == 1)
                    {
                        mess3 += GetMessageDetail(drMaster);
                        DTDKLopIDs += " DTDKID = '" + drMaster["ID", DataRowVersion.Original] + "' AND";
                    }
                }
            }
            if (Mess != "")
            {
                XtraMessageBox.Show("Học viên đã được duyệt, không cho phép sửa/xóa dữ liệu! \nNhững dòng dữ liệu sau không được phép sửa/xóa : \n" + Mess, Config.GetValue("PackageName").ToString());
                _info.Result = false;
            }
            if (mess2 != "")
            {
                XtraMessageBox.Show("Không cho phép sửa/xóa dữ liệu!\nNhững dòng thu phí sau đã đưa vào danh sách đề nghị khai giảng(trình duyệt/đã duyệt)\n" + mess2, Config.GetValue("PackageName").ToString());
                _info.Result = false;
            }
            if (mess3 != "")
            {
                if (XtraMessageBox.Show("Những dòng thu phí sau đã đưa vào danh sách đề nghị khai giảng\nBạn vẫn muốn xóa những thu phí sau khỏi đề nghị khai giảng ?\n" + mess3, Config.GetValue("PackageName").ToString(), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    DTDKLopIDs = DTDKLopIDs.Substring(0,DTDKLopIDs.Length - 3);
                    string sql = "Delete From DTDNKG Where " + DTDKLopIDs;
                    _data.DbData.UpdateByNonQuery(sql);
                }
                else
                    _info.Result = false;
            }
            //}
            return _info.Result;
        }
        private string GetMessageDetail(DataRow dr)
        {
            string NgayDK = "";
            string MaLop = "";
            if (drMaster.RowState == DataRowState.Deleted || drMaster.RowState == DataRowState.Modified)
            {
                NgayDK = ((DateTime)dr["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy");
                MaLop = dr["MaLop", DataRowVersion.Original].ToString();
            }
            else
            {
                NgayDK = ((DateTime)dr["NgayTN"]).ToString("dd/MM/yyyy");
                MaLop = dr["MaLop"].ToString();
            }
            return string.Format("- Dòng thu học phí của học viên [{0}] thuộc lớp [{1}] có ngày thu [{2}]", hvInfo.tenHV, MaLop, NgayDK);
        }
        private bool KiemTraBeforePhieuThus()
        {
            // kiểm tra các phiếu MT11 or MT15 đã duyệt chưa , đã duyệt thì không được xóa/sửa.
            if (!KiemTraChungTu())
                return _info.Result;
            string Mess = "";            
            //foreach (DataRowView drv in dvDTDKLop)
            //{

            if (drMaster.RowState != DataRowState.Deleted)
            {
                // nếu có nộp tiền
                if (drMaster["TDaNop"] != DBNull.Value
                     && (decimal)drMaster["TDaNop"] != 0)
                {
                    // nếu có mua giáo trình thì phải chọn cấp độ
                    if ((bool)drMaster["isMuaBT"] && drMaster["MaCD"] == DBNull.Value)
                        Mess += "- Vui lòng chọn cấp độ cho những đăng ký lớp có mua giáo trình thuộc:\n";
                }
                if (Mess != "")
                {
                    XtraMessageBox.Show(Mess + GetMessageDetail(drMaster), Config.GetValue("PackageName").ToString());
                    _info.Result = false;
                    return _info.Result;
                }
            }
            //}
            return _info.Result;
        }
        private void InsertTinhTrang(string hvtvid, DateTime ngay, int tinhTrangID, string manv, bool isDKL)
        {
            string tinhtrangCu = "Đang bảo lưu"; string mota = "";
            if (isDKL)
                mota = string.Format("Chuyển tình trạng: {0} sang Đã xếp lớp", tinhtrangCu);
            else
                mota = string.Format("Chuyển tình trạng: Đã xếp lớp sang {0}", tinhtrangCu);

            string sql1 = @"insert	into dttinhtrang(hvid,ngay,tinhtrangid,mota,manv)
		                            values(@HVTVID,@Ngay,@TinhTrangID,@MoTa,@MaNV)";
            string[] paraName = new string[] { "@HVTVID", "@Ngay", "@TinhTrangID", "@MoTa", "@MaNV" };
            object[] paraObj = new object[] { hvtvid, ngay, tinhTrangID, mota, manv };
            db.UpdateDatabyPara(sql1, paraName, paraObj);

            //cập nhật tình trạng trong dmhvtv
            string sql2 = @"update dmhvtv set tinhtrang = " + tinhTrangID.ToString() + " where hvtvid = " + hvtvid;
            db.UpdateByNonQuery(sql2);
        }
        public class HocVienInfos
        {
            public string tenHV = "";
            public string maCN = "";
            public string maHV = "";
        }
        public static class TinhTrangs
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
    }
}
