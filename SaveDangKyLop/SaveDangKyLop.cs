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
using DangKyLopICC;

namespace SaveDangKyLop
{
    public class SaveDangKyLop : ICData
    {
        private Database db = Database.NewDataDatabase();
        private InfoCustomData _info;
        private DataCustomData _data;
        private DataRow drMaster;
        private int HVTVID;
        private DateTime DateNow = DateTime.Now;
        private string MaNV = Config.GetValue("UserName").ToString();
        private string ListChanges_DTDKLopID = "";
        private QuanLyCT quanlyCT;
        int TinhTrangDauTien;        
         
        public SaveDangKyLop()
        {
            _info = new InfoCustomData(IDataType.MasterDetailDt);
        }

        public SaveDangKyLop(DataRow _drMaster)
        {
            _info = new InfoCustomData(IDataType.MasterDetailDt);
            drMaster = _drMaster;
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
            if (_data.CurMasterIndex < 0)
                return;
            drMaster = _data.DsDataCopy.Tables[0].Rows[_data.CurMasterIndex];
            if (drMaster.RowState == DataRowState.Deleted)
                return;
            HVTVID = GetHVTVID();

            if (drMaster.RowState == DataRowState.Added)
            {
                TinhTrangDauTien = (int)drMaster["TinhTrang"];
                // có chọn lịch học
                //if (drMaster["GioHoc"] != DBNull.Value)
                //    drMaster["TinhTrang"] = TinhTrangs.DangChoXepLop;
            }

            //DataView dvDTDKLop = GetDetailView();
            using (DataView dvDTDKLop = GetDetailView())
            {
                // TIẾN HÀNH CẬP NHẬT DỮ LIỆU
                UpdateAllDatas(HVTVID, dvDTDKLop);
            }
        }
        public void ExecuteBefore()
        {
            if (_data.CurMasterIndex < 0)
                return;
            _info.Result = true;
            drMaster = _data.DsData.Tables["DMHVTV"].Rows[_data.CurMasterIndex];
            HVTVID = GetHVTVID();
            
            // Code tạo MaHV này chỉ dùng cho lúc Đức đang test , chứ MaHV không phải tạo như thế này và tạo ở đây.
            CreateMaHV();
            
             
            // kiểm tra thông tin đăng ký học thử
            if (!KiemTraHocThuInfos())
                return;

            using (DataView dvDTDKLop = GetDetailView())
            {
                /*-- Yếu cầu 1:
                 * KIểm tra xem các đăng ký lớp đã được duyệt hay chưa nếu duyệt rồi thì không cho phép sửa/xóa dữ liệu trong DTDKLop  --*/
                #region Thực hiện YÊU CẦU 1
                List<string> list_MTDKsWillDelete = new List<string>();
                if (!KiemTraDangKyLop(dvDTDKLop, ref list_MTDKsWillDelete))
                    return;
                #endregion


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

                if (!KiemTraBeforePhieuThus(dvDTDKLop))
                    return;

                #endregion


                if (drMaster.RowState == DataRowState.Deleted)
                {
                    DeleteAllDatasBefore(list_MTDKsWillDelete);
                }

            }
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
                                Exec SP_DeleteAllDatasFromForeignTable 'MTDK','{0}'
                                Delete From MTDK Where HVID = '{0}'", item);
                }
            }
            //
            string MaKH_HS = drMaster["MaHV", DataRowVersion.Original].ToString();
            sql1 += string.Format(@"
                        delete from bltk where makh = '{0}'
                        delete from blvt where makh = '{0}'
                        delete from dt43 where mt43id in (select mt43id from mt43 where MaKH = '{0}')
                        delete from mt43 where MaKH = '{0}'
                        delete from dt15 where MaKHCt = '{0}'
                        delete from mt15 where MaKH = '{0}'
                        delete from dt11 where MaKHCt = '{0}'
                        delete from mt11 where MaKH = '{0}'", MaKH_HS);
            // xóa những dữ liệu có tham chiếu đến DMHVTV
            sql1 += string.Format(@"
                Exec SP_DeleteAllDatasFromForeignTable 'DMHVTV','{0}'", HVTVID);
            if (sql1 != "")
                _data.DbData.UpdateByNonQuery(sql1);
        }
        private void UpdateAllDatas(int HVTVID, DataView dvDTDKLop)
        {
            // Cập nhật học thử
            UpdateHocThu(dvDTDKLop);

            // UDPATE MTDK.               
            UpdateMTDK(dvDTDKLop);

            // UPDATE PHIẾU THUS
            UpdatePhieuThus(dvDTDKLop);

            // CẬP NHẬT TIỀN BẢO LƯU CHO MTDK
            // nếu Tổng tiền BL của HV có thay đổi khi user nhấn vào xử lý nút [Tính tiền bảo lưu]
            if (DangKyLopICC.DangKyLopICC.m_TongTienBL != -1 && DangKyLopICC.DangKyLopICC.s_MTDKID != "")
            {
                string sql = string.Format(@"                    
                    Update MTDK Set BLSoTien = {1} Where HVID = '{0}'"
                    , DangKyLopICC.DangKyLopICC.s_MTDKID, DangKyLopICC.DangKyLopICC.m_TongTienBL);
                _data.DbData.UpdateByNonQuery(sql);
            }

            // UPDATE CÁC DÒNG DTDKLOP KHI CÓ THAY ĐỔI NHỮNG FIELDS SAU : 
            // REFMTDK OR MT31ID OR MT11ID OR MT15ID OR MT43ID
            UpdateDTDKLop(dvDTDKLop);

            // UPDATE TÌNH TRẠNG HỌC VIÊN
            UpdateTinhTrangHV();
        }
        private void UpdateTinhTrangHV()
        {
            if (drMaster.RowState == DataRowState.Modified
                || drMaster.RowState == DataRowState.Added)
            {
                int TinhTrang_Ori;
                int TinhTrang_Cur = (int)drMaster["TinhTrang"];                

                if (drMaster.RowState == DataRowState.Modified
                    && ((int)drMaster["TinhTrang", DataRowVersion.Original] != TinhTrang_Cur))
                {
                    TinhTrang_Ori = (int)drMaster["TinhTrang", DataRowVersion.Original];
                    XuLy_UpdateTinhTrangHV(TinhTrang_Ori, TinhTrang_Cur);
                }
                if (drMaster.RowState == DataRowState.Added)
                    XuLy_UpdateTinhTrangHV(TinhTrangDauTien, TinhTrang_Cur);

            }
        }
        private void XuLy_UpdateTinhTrangHV(int TinhTrang_Ori, int TinhTrang_Cur)
        {
            if (TinhTrang_Cur == -1)
                return;
            _data.DbData.EndMultiTrans();
            db.BeginMultiTrans();
            string MoTa = "";
            string sql = string.Format(@"
                        Select N'Chuyển tình trạng: ' + old.TinhTrang + ' sang ' + new.TinhTrang From DMTinhTrang old,DMTinhTrang new Where old.ID = {0} and new.ID = {1}"
                                   , TinhTrang_Ori, TinhTrang_Cur);
            
            MoTa = db.GetValue(sql).ToString();
            sql = string.Format(@"
                        Declare @MoTa nvarchar(128)                        
                        Insert Into DTTinhTrang(HVID,Ngay,TinhTrangID,MaNV,MoTa)
                        Values({0},'{1}',{3},'{4}',N'{5}')
                            
                        Update DMHVTV Set TinhTrang  = {3} Where HVTVID = {0}"
                , HVTVID, DateNow, TinhTrang_Ori, TinhTrang_Cur, MaNV, MoTa);

            if (db.UpdateByNonQuery(sql))
                db.EndMultiTrans();
            else
            {
                _info.Result = false;
                db.RollbackMultiTrans();
            }
            DataRow drMasters = _data.DsData.Tables["DMHVTV"].Rows[_data.CurMasterIndex];
            drMasters["TinhTrang"] = TinhTrang_Cur;
            DataRow dr = _data.DsData.Tables["DTTinhTrang"].NewRow();
            dr["HVID"] = HVTVID;
            dr["Ngay"] = DateNow;
            dr["MoTa"] = MoTa;
            dr["MaNV"] = MaNV;
            dr["TinhTrangID"] = TinhTrang_Cur;            
            _data.DsData.Tables["DTTinhTrang"].Rows.Add(dr);            
        }
        private void UpdateMTDK(DataView dvDTDKLop)
        {
            if (dvDTDKLop.Count > 0)
            {
                string DTDKLopID = "";
                string MaLop = "";
                string MAHVTV = drMaster["MaHV"].ToString();
                string TenHV = drMaster["TenHV"].ToString();
                string MACNDK = drMaster["MaCN"].ToString();
                string MALOP_NEW = "";
                string MANHOMLOP = "";
                string newMTDKID = Guid.NewGuid().ToString();
                string NgayDK = "";
                string NgaySinh = "";

                foreach (DataRowView drv in dvDTDKLop)
                {
                    GetValueForParams(drv.Row, ref newMTDKID, ref DTDKLopID, ref MaLop, ref MALOP_NEW, ref MANHOMLOP, ref NgaySinh, ref NgayDK);

                    string sql = "";
                    // thêm mới
                    if (drv.Row.RowState == DataRowState.Added)
                    {
                        if (drv.Row["MaLop"] != DBNull.Value)
                        {
                            //update tiền bảo lưu trong mtdk và trạng thái hvtv
                            //Update_DMHVTV_MTDK(drv.Row, "THEM");

                            // Insert MTDK
                            if (!KiemTraDangHocThuByMaLop(drv, MALOP_NEW, MANHOMLOP, DTDKLopID))
                                continue;

                            if (!KiemTraDangKyLanDau(MALOP_NEW, ""))
                                continue;

                            drv.Row["refMTDK"] = newMTDKID;
                            sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','NEW','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);

                            drMaster["TinhTrang"] = TinhTrangs.DaXepLop;

                            // Lưu lại những dòng có xử lý thay đổi 1 trong những field sau refMTDK,MT31,MT11,MT15,MT43 
                            // để update dữ liệu vào DTDKLop.
                            // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                            GetListChanges_DTDKLopID(drv);
                        }
                        else
                            drMaster["TinhTrang"] = TinhTrangs.DangChoXepLop;
                    }
                    // sửa
                    if (drv.Row.RowState == DataRowState.Modified)
                    {
                        if (drv.Row["MaLop", DataRowVersion.Original] == drv.Row["MaLop"]
                            && drv.Row["NgayTN", DataRowVersion.Original] == drv.Row["NgayTN"]
                            && drv.Row["NgayDK", DataRowVersion.Original] == drv.Row["NgayDK"]
                            && drv.Row["MaCD", DataRowVersion.Original] == drv.Row["MaCD"])
                            continue;

                        // Trước khi sửa có MaLop, sau khi sửa có MaLop
                        // Nếu chỉ MaLop or NgayDK or MaCD thay đổi
                        if (drv.Row["MaLop"] != DBNull.Value && drv.Row["MaLop", DataRowVersion.Original] != DBNull.Value)
                        {
                            if (drv.Row["MaLop", DataRowVersion.Original].ToString() != drv.Row["MaLop"].ToString()
                                   || drv.Row["NgayTN", DataRowVersion.Original].ToString() != drv.Row["NgayTN"].ToString()
                                   || drv.Row["NgayDK", DataRowVersion.Original].ToString() != drv.Row["NgayDK"].ToString()
                                     || drv.Row["MaCD", DataRowVersion.Original].ToString() != drv.Row["MaCD"].ToString())
                            {
                                // Lưu lại những dòng có xử lý thay đổi 1 trong những field sau refMTDK,MT31,MT11,MT15,MT43 
                                // để update dữ liệu vào DTDKLop.
                                // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                                GetListChanges_DTDKLopID(drv);

                                if (drv.Row["MaLop", DataRowVersion.Original].ToString() != drv.Row["MaLop"].ToString())
                                {
                                    // nếu đã đăng ký vào MTDK với lớp A rồi thì không cần Insert thêm vào MTDK nữa
                                    // nếu DTDKLop này đang giữ 1 MTDK nào đó (field refMTDK có giá trị) với Lớp A
                                    // bây giờ chuyển sang Lớp B mà Lớp B thì đã đăng ký rồi nên khi chuyển như vậy
                                    // phải tiến hành xóa MTDK (lớp A) mà DTDKLop này đang giữ.

                                    if (!KiemTraDangHocThuByMaLop(drv, MALOP_NEW, MANHOMLOP, DTDKLopID))
                                        continue;

                                    // đã đăng ký lớp
                                    if (!KiemTraDangKyLanDau(MALOP_NEW, MaLop))
                                    {
                                        if (drv.Row["refMTDK"] != DBNull.Value)
                                        {
                                            sql = string.Format(@"
                                                -- Delete tất cả dữ liệu liên quan đến MTDK                                    
                                                Exec SP_DeleteAllDatasFromForeignTable 'MTDK','{0}'
                                                DELETE FROM MTDK WHERE HVID = '{0}' -- AND DUYET = 0"
                                                , drv.Row["refMTDK"]);
                                            drv.Row["refMTDK"] = DBNull.Value;
                                        }
                                    }
                                    // chưa đăng ký lớp
                                    else
                                    {
                                        drv.Row["refMTDK"] = newMTDKID;
                                        sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','EDIT','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                                            , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);
                                    }
                                }
                                // nếu không phải thay đổi MaLop thì xem DTDKLop có giá trị refMTDK không (tức có đang giữ MTDK không)
                                // nếu có thì mới tiến hành thay đổi cho MTDK đó.
                                else if (drv.Row["refMTDK"] != DBNull.Value)
                                {
                                    drv.Row["refMTDK"] = newMTDKID;
                                    sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','EDIT','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                                    , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);
                                }
                            }
                            drMaster["TinhTrang"] = TinhTrangs.DaXepLop;
                        }
                        // Trước khi sửa có MaLop, sau khi sửa không có MaLop
                        if (drv.Row["MaLop"] == DBNull.Value && drv.Row["MaLop", DataRowVersion.Original] != DBNull.Value)
                        {
                            drv.Row["refMTDK"] = DBNull.Value;
                            sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','DELETE','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                                , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);
                            // Lưu lại những dòng có xử lý thay đổi 1 trong những field sau refMTDK,MT31,MT11,MT15,MT43 
                            // để update dữ liệu vào DTDKLop.
                            // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                            GetListChanges_DTDKLopID(drv);

                            drMaster["TinhTrang"] = TinhTrangs.DangChoXepLop;
                        }
                        // Trước khi sửa không có MaLop, sau khi sửa có MaLop
                        if (drv.Row["MaLop"] != DBNull.Value && drv.Row["MaLop", DataRowVersion.Original] == DBNull.Value)
                        {
                            // nếu đã đăng ký vào MTDK với lớp A rồi thì không cần Insert thêm vào MTDK nữa
                            if (!KiemTraDangHocThuByMaLop(drv, MALOP_NEW, MANHOMLOP, DTDKLopID))
                                continue;
                            
                            if (!KiemTraDangKyLanDau(MALOP_NEW, ""))
                                continue;
                            drv.Row["refMTDK"] = newMTDKID;
                            sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','NEW','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                                , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);

                            // Lưu lại những dòng có xử lý thay đổi 1 trong những field sau refMTDK,MT31,MT11,MT15,MT43 
                            // để update dữ liệu vào DTDKLop.
                            // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                            GetListChanges_DTDKLopID(drv);

                            drMaster["TinhTrang"] = TinhTrangs.DaXepLop;
                        }
                    }
                    // xóa
                    if (drv.Row.RowState == DataRowState.Deleted)
                    {
                        sql = string.Format("Exec SP_InsertMTDK '{0}',{1},'{2}','DELETE','{3}',N'{4}','{5}','{6}','{7}','{8}','{9}',1,'{10}';"
                                                , MaLop, HVTVID, DTDKLopID, MAHVTV, TenHV, NgayDK, MANHOMLOP, MACNDK, MALOP_NEW, NgaySinh, newMTDKID);
                        //Update_DMHVTV_MTDK(drv.Row, "XOA");
                    }
                    // THỰC THI
                    if (sql != "")
                    {
                        _data.DbData.UpdateByNonQuery(sql);
                    }
                }
            }
        }
        private bool KiemTraDangHocThuByMaLop(DataRowView drv, string MaLop, string MaCD, string DTDKLopID)
        {
            //  Nếu đang học thử thì lấy MTDKID của MTDK đang học thử gắn wa cho thu phí với lớp đó.
            string sql = string.Format(@"
                Declare @refMTDK nvarchar(128)
                Select @refMTDK = HVID From MTDK Where HVTVID = {0} And MaLop = '{1}' and isDKL = 1 --- isDKL:Học thử
                If @refMTDK is null
                    Select 0 
                Else
                Begin
	                Declare @MaCD nvarchar(128)
	                Set @MaCD = '{2}'
	                If @MaCD = '' 
                        Set @MaCD = NULL
		            Update MTDK Set refDTDKLop = '{3}', MaNhomLop = @MaCD,isNghiHoc = 0,NgayNghi = null, isDKL = 0 Where HVID = @refMTDK
	                Select @refMTDK
                End", HVTVID, MaLop, MaCD, DTDKLopID);
            string refMTDK = _data.DbData.GetValue(sql).ToString();
            if (refMTDK != "0")
            {
                drv.Row["refMTDK"] = refMTDK;
                drMaster["TinhTrang"] = TinhTrangs.DaXepLop;
                GetListChanges_DTDKLopID(drv);
                return false;
            }
            return true;
        }
        private void UpdatePhieuThus(DataView dvDTDKLop)
        {
            if (drMaster.RowState == DataRowState.Deleted)
                quanlyCT = new QuanLyCT(drMaster["MaHV", DataRowVersion.Original].ToString(), drMaster["TenHV", DataRowVersion.Original].ToString(), _data.DbData);
            else
                quanlyCT = new QuanLyCT(drMaster["MaHV"].ToString(), drMaster["TenHV"].ToString(), _data.DbData);

            foreach (DataRowView drv in dvDTDKLop)
            {
                // thêm
                if (drv.Row.RowState == DataRowState.Added)
                {
                    // nếu có nộp tiền thì mới tạo phiếu thus
                    if (drv.Row["HTTT"] != DBNull.Value && (decimal)drv.Row["TDaNop"] > 0)
                    {
                        Insert_MT11_MT15_MT43_BLTK_BLVT(drv, true);
                        // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                        // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                        GetListChanges_DTDKLopID(drv);
                    }                                        
                }
                // Sửa..
                // Ở đây không sử dụng table.Getchanges(state) để lấy ra table chứa những dòng modified vì 
                // không phải bất cứ sự thay đổi nào trên những dòng đó đều lấy ra modified
                // mà chỉ lấy khi 1 số fields bên dứoi thay đổi.
                if (drv.Row.RowState == DataRowState.Modified)
                {
                    if (drv.Row["NgayTN"].ToString() != drv.Row["NgayTN", DataRowVersion.Original].ToString()
                        || drv.Row["HTTT"].ToString() != drv.Row["HTTT", DataRowVersion.Original].ToString()
                        //|| drv.Row["TienGT"].ToString() != drv.Row["TienGT", DataRowVersion.Original].ToString()
                        //|| drv.Row["TienHP"].ToString() != drv.Row["TienHP", DataRowVersion.Original].ToString()
                        || drv.Row["TDaNop"].ToString() != drv.Row["TDaNop", DataRowVersion.Original].ToString())
                    {
                        Update_MT11_MT15_MT43_BLTK_BLVT(drMaster, drv);
                        // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                        // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                        GetListChanges_DTDKLopID(drv);
                    }
                }
                // xóa
                if (drv.Row.RowState == DataRowState.Deleted)
                {
                    quanlyCT.Delete_MT11_MT15_MT43_BLTK_BLVT(drv.Row);
                    // Lưu lại những dòng có xử lý để update dữ liệu vào DTDKLop
                    // tránh việc dữ liệu được gán vào cell nhưng chưa thật sự lưu xuống database.
                    GetListChanges_DTDKLopID(drv);
                }
            }
            //decimal a = DangKyLopICC.DangKyLopICC.f_TongTienBL;            
        }
        private void UpdateHocThu(DataView dv)
        {
            string sql = "";
            int CountDTDKlop = -1;
            CountDTDKlop = GetCountDTDKLopCurrentRowsByMaLop();
            if (drMaster.RowState == DataRowState.Added)
            {
                if (drMaster["LopHocThu"] != DBNull.Value)
                {
                    sql += GetSQL_InsertHocThu(drMaster["LopHocThu"].ToString(), "", (int)drMaster["SoBuoiHT"], CountDTDKlop, "ADD");
                    drMaster["TinhTrang"] = TinhTrangs.DangHocThu;
                }
            }
            if (drMaster.RowState == DataRowState.Modified)
            {
                int TinhTrangID = -1;                

                // không có lớp HT --> có lớp
                if (drMaster["LopHocThu", DataRowVersion.Original] == DBNull.Value
                    && drMaster["LopHocThu"] != DBNull.Value)
                {
                    sql += GetSQL_InsertHocThu(drMaster["LopHocThu"].ToString(), "", (int)drMaster["SoBuoiHT"], CountDTDKlop, "ADD");
                    TinhTrangID = TinhTrangs.DangHocThu;
                }
                // có lớp HT --> không có lớp
                if (drMaster["LopHocThu", DataRowVersion.Original] != DBNull.Value
                    && drMaster["LopHocThu"] == DBNull.Value)
                {
                    sql += GetSQL_InsertHocThu(drMaster["LopHocThu"].ToString(), drMaster["LopHocThu", DataRowVersion.Original].ToString(), 0, CountDTDKlop, "DELETE");
                    TinhTrangID = TinhTrangs.DangChoXepLop;
                }
                // có lớp --> có lớp
                if (drMaster["LopHocThu", DataRowVersion.Original] != DBNull.Value
                    && drMaster["LopHocThu"] != DBNull.Value)
                {
                    sql += GetSQL_InsertHocThu(drMaster["LopHocThu"].ToString(), drMaster["LopHocThu", DataRowVersion.Original].ToString(), (int)drMaster["SoBuoiHT"], CountDTDKlop, "EDIT");
                    TinhTrangID = TinhTrangs.DangHocThu;
                }
                // nếu có lớp học thiệt rồi thì không cho thay đổi tình trạng HV khi chỉnh sửa thông tin học thử.
                if (AllowChangeTinhTrang() && TinhTrangID != -1)
                    drMaster["TinhTrang"] = TinhTrangID;
            }            
            if (sql != "")
                _data.DbData.UpdateByNonQuery(sql);

        }
        private bool AllowChangeTinhTrang()
        {
            string sql = string.Format("If Exists (Select HVID From MTDK Where HVTVID = {0} and isDKL = 0) Select 1 Else Select 0", HVTVID);
            if ((int)_data.DbData.GetValue(sql) == 0)
                return true;
            return false;
        }
        private int GetCountDTDKLopCurrentRowsByMaLop()
        {
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
                return GetDetailViewCurrentRows().ToTable().Select("MaLop = '" + drMaster["LopHocThu", DataRowVersion.Original] + "'").Length;
            return -1;
        }
        private void UpdateDTDKLop(DataView dvDTDKLop)
        {
            if (ListChanges_DTDKLopID == "")
                return;
            ListChanges_DTDKLopID = ListChanges_DTDKLopID.Substring(0, ListChanges_DTDKLopID.Length - 2);
            dvDTDKLop.RowFilter = ListChanges_DTDKLopID;
            if (dvDTDKLop.Count == 0)
                return;
            _data.DbData.EndMultiTrans();
            db.BeginMultiTrans();
            string sql = @"
                DECLARE @REFMTDK NVARCHAR(128)
                DECLARE @MT31ID NVARCHAR(128)
                DECLARE @MT11ID NVARCHAR(128)
                DECLARE @MT15ID NVARCHAR(128)
                DECLARE @MT43ID NVARCHAR(128)";
            foreach (DataRowView drv in dvDTDKLop)
            {
                sql += string.Format(@"
                    SET @REFMTDK = '{1}'
                    SET @MT31ID = '{2}'
                    SET @MT11ID = '{3}'
                    SET @MT15ID = '{4}'
                    SET @MT43ID = '{5}'

                    IF @REFMTDK = ''
                        SET @REFMTDK = NULL
                    IF @MT31ID = ''
                        SET @MT31ID = NULL
                    IF @MT11ID = ''
                        SET @MT11ID = NULL
                    IF @MT15ID = ''
                        SET @MT15ID = NULL
                    IF @MT43ID = ''
                        SET @MT43ID = NULL
                    UPDATE DTDKLOP 
                    SET REFMTDK = @REFMTDK, MT31ID = @MT31ID, MT11ID = @MT11ID, MT15ID = @MT15ID, MT43ID = @MT43ID
                    WHERE ID = '{0}'"
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["ID"] : drv.Row["ID", DataRowVersion.Original]
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["refMTDK"] : drv.Row["refMTDK", DataRowVersion.Original]
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["MT31ID"] : drv.Row["MT31ID", DataRowVersion.Original]
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["MT11ID"] : drv.Row["MT11ID", DataRowVersion.Original]
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["MT15ID"] : drv.Row["MT15ID", DataRowVersion.Original]
                    , drv.Row.RowState != DataRowState.Deleted ? drv.Row["MT43ID"] : drv.Row["MT43ID", DataRowVersion.Original]);
            }
            if (db.UpdateByNonQuery(sql))
                db.EndMultiTrans();
            else
            {
                _info.Result = false;
                db.RollbackMultiTrans();
            }
        }
        private void GetListChanges_DTDKLopID(DataRowView drv)
        {
            string DTDKLopID = drv.Row.RowState != DataRowState.Deleted ? drv.Row["ID"].ToString() : drv.Row["ID", DataRowVersion.Original].ToString();
            if (!ListChanges_DTDKLopID.Contains(DTDKLopID))
                ListChanges_DTDKLopID += " ID = '" + DTDKLopID + "' OR";
        }
        private void GetValueForParams(DataRow dr, ref string newMTDKID, ref string DTDKLopID, ref string MaLop, ref string MALOP_NEW, ref string MANHOMLOP, ref string NgaySinh, ref string NgayDK)
        {
            DTDKLopID = "";
            MaLop = "";
            MALOP_NEW = "";
            MANHOMLOP = "";
            newMTDKID = Guid.NewGuid().ToString();
            NgayDK = "";
            NgaySinh = "";

            if (dr.RowState == DataRowState.Deleted)
                DTDKLopID = dr["ID", DataRowVersion.Original].ToString();
            else
            {
                DTDKLopID = dr["ID"].ToString();
                NgayDK = ((DateTime)dr["NgayTN"]).ToString("yyyy/MM/dd");
                if (dr["MaCD"] != DBNull.Value)
                    MANHOMLOP = dr["MaCD"].ToString();
            }
            if (drMaster["NgaySinh"] != DBNull.Value)
                NgaySinh = ((DateTime)drMaster["NgaySinh"]).ToString("yyyy/MM/dd");
            if ((dr.RowState == DataRowState.Modified || dr.RowState == DataRowState.Deleted)
                && dr["MaLop", DataRowVersion.Original] != DBNull.Value)
                MaLop = dr["MaLop", DataRowVersion.Original].ToString();
            if (dr.RowState != DataRowState.Deleted && dr["MaLop"] != DBNull.Value)
                MALOP_NEW = dr["MaLop"].ToString();
        }
        private bool CheckPhieus(DataView dv)
        {
            string Message = "";
            string msgThuNhieuLan = "";
            foreach (DataRowView drv in dv)
            {
                if (drv.Row.RowState == DataRowState.Deleted || 
                    (drv.Row.RowState == DataRowState.Modified && (
                           drv.Row["NgayTN"].ToString() != drv.Row["NgayTN", DataRowVersion.Original].ToString()
                        || drv.Row["HTTT"].ToString() != drv.Row["HTTT", DataRowVersion.Original].ToString()
                        || drv.Row["MaCD"].ToString() != drv.Row["MaCD", DataRowVersion.Original].ToString()
                        || drv.Row["TienHP"].ToString() != drv.Row["TienHP", DataRowVersion.Original].ToString()
                        || drv.Row["TDaNop"].ToString() != drv.Row["TDaNop", DataRowVersion.Original].ToString()
                       )
                    ))
                {
                    string MT11 = drv.Row["MT11ID", DataRowVersion.Original] != DBNull.Value ? drv.Row["MT11ID", DataRowVersion.Original].ToString() : "";
                    string MT15 = drv.Row["MT15ID", DataRowVersion.Original] != DBNull.Value ? drv.Row["MT15ID", DataRowVersion.Original].ToString() : "";
                    string MT31 = drv.Row["MT31ID", DataRowVersion.Original] != DBNull.Value ? drv.Row["MT31ID", DataRowVersion.Original].ToString() : "";
                    string MT43 = drv.Row["MT43ID", DataRowVersion.Original] != DBNull.Value ? drv.Row["MT43ID", DataRowVersion.Original].ToString() : "";
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
                    if ((int)db.GetValue(sql) == 1)
                        Message += string.Format("- Dòng thu học phí có ngày thu :{0}", ((DateTime)drv.Row["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy"));
                    DataTable dtCTThuPhi = db.GetDataTable(string.Format("select ID from CTThuTien where DTDKLop = '{0}'", drv.Row["ID", DataRowVersion.Original]));
                    if (dtCTThuPhi.Rows.Count > 0)
                        msgThuNhieuLan += string.Format("- Dòng thu học phí có ngày thu:{0}", ((DateTime)drv.Row["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy"));
                }
            }
            // phiếu MT11 or MT15 or MT31 đã duyệt thì không được xóa/sửa
            if (Message != "")
            {
                XtraMessageBox.Show("Phiếu thu học phí đã được duyệt, không cho phép sửa/xóa dữ liệu !\nVui lòng kiểm tra dữ liệu tại những dòng sau thuộc Tab 'Thu học phí' :\n" + Message, Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return false;
            }
            else
                // hoặc nếu đã thu học phí lần 2 trở đi thì cũng không được xóa/sửa
                if (msgThuNhieuLan != "")
                {
                    XtraMessageBox.Show("Đã thu học phí nhiều lần, không cho phép sửa/xóa dữ liệu !\nVui lòng kiểm tra dữ liệu tại những dòng sau thuộc Tab 'Thu học phí' :\n" + msgThuNhieuLan, Config.GetValue("PackageName").ToString());
                    _info.Result = false;
                    return false;
                }
            return true;
        }
        private void Update_MT11_MT15_MT43_BLTK_BLVT(DataRow drmaster, DataRowView drv)
        {
            // Xóa dữ liệu cũ
            quanlyCT.Delete_MT11_MT15_MT43_BLTK_BLVT(drv.Row);

            // Insert mới các phiếu
            if (drv.Row["HTTT"] != DBNull.Value && (decimal)drv.Row["TDaNop"] > 0)
            {
                Insert_MT11_MT15_MT43_BLTK_BLVT(drv, true);
            }
        }
        public void Delete_MT11_MT15_MT43_BLTK_BLVT(DataRowView drv)
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
                    , drv.Row["MT31ID", DataRowVersion.Original]
                    , drv.Row["MT11ID", DataRowVersion.Original]
                    , drv.Row["MT15ID", DataRowVersion.Original]
                    , drv.Row["MT43ID", DataRowVersion.Original]
                    , drv.Row["ID", DataRowVersion.Original]);
            if (drv.Row.RowState != DataRowState.Deleted)
            {
                drv.Row["MT11ID"] = DBNull.Value;
                drv.Row["MT15ID"] = DBNull.Value;
                drv.Row["MT31ID"] = DBNull.Value;
                drv.Row["MT43ID"] = DBNull.Value;
            }
            // thực thi
            _data.DbData.UpdateByNonQuery(sql);
        }
        private void Insert_MT11_MT15_MT43_BLTK_BLVT(DataRowView drv, bool isInsertMT43)
        {
            if (drv.Row["HTTT"] != DBNull.Value && (decimal)drv.Row["TDaNop"] > 0)
            {
                string sql = "";
                decimal TDaNop = (decimal)drv.Row["TDaNop"];
                //QuanLyCT ct = new QuanLyCT(drMaster["MaHV"].ToString(), drMaster["TenHV"].ToString(), _data.DbData);
                // không phải là dịch vụ liên kết
                if (drMaster["MaCN"].ToString() != "DVLK")
                {
                    // Thanh toán bằng tiền mặt
                    if (drv.Row["HTTT"].ToString() == "TM" && TDaNop > 0)
                    {
                        sql += quanlyCT.GetStringInsertMT11_BLTK(drv.Row, isInsertMT43);
                    }
                    // Không phải thanh toán bằng tiền mặt
                    else
                    {
                        sql += quanlyCT.GetStringInsertMT15_BLTK(drv.Row, isInsertMT43);
                    }
                }
                // dịch vụ liên kết
                else
                    sql += quanlyCT.GetStringInsertMT31_BLTK(drv.Row, isInsertMT43);
                // thực thi
                _data.DbData.UpdateByNonQuery(sql);
            }
        }
        private string GetSQL_InsertHocThu(string LopHT, string LopHT_Ori, int SoBuoiHT, int CountDTDKLop, string Mode)
        {
            string sql = "";
            sql = string.Format(@"
                    DECLARE @HVTVID         INT
                    DECLARE @MODE           NVARCHAR(128)
                    DECLARE @LOPHOCTHU      NVARCHAR(128)
                    DECLARE @LOPHOCTHU_ORI  NVARCHAR(128)  
                    DECLARE @MAHVTV         NVARCHAR(128)
                    DECLARE @TENHV          NVARCHAR(128)		            	
                    DECLARE @MACNDK         NVARCHAR(128)
                    DECLARE @MALOP_NEW      NVARCHAR(128)
                    DECLARE @NGAYSINH       DATETIME
                    DECLARE @NGAYNGHI       DATETIME
                    DECLARE @NGAYDK         DATETIME                                       
                    DECLARE @NGAYHOCTHU     DATETIME
                    DECLARE @COUNTDTDKLOP   INT                    

                    SET @HVTVID = {0}
                    SET @LOPHOCTHU = '{1}'
                    SET @LOPHOCTHU_ORI = '{2}'
                    SET @MODE = '{5}'
                    SET @COUNTDTDKLOP = {4}
                    SET @MACNDK = '{6}'
                    SET @TENHV = N'{7}'
                    SET @MAHVTV = '{8}'
                    SET @NGAYHOCTHU = '{9}'
                    SET @NGAYSINH = '{10}'

                    IF @MODE = 'EDIT' OR @MODE = 'DELETE'
                    BEGIN	
                        -- NẾU CHƯA THU PHÍ(CÓ LỚP TRÙNG VỚI LỚP HỌC THỬ) THÌ MỚI ĐƯỢC XÓA MTDK HỌC THỬ
                        -- IF NOT EXISTS (SELECT TOP 1 ID FROM DTDKLOP WHERE HVTVID = @HVTVID AND MALOP = @LOPHOCTHU_ORI)
                        IF(@COUNTDTDKLOP <= 0)
	                    BEGIN		                    
		                    DECLARE @MTDKID NVARCHAR(100)
		                    SELECT @MTDKID FROM MTDK WHERE HVTVID = @HVTVID AND MALOP = @LOPHOCTHU_ORI AND ISDKL = 1 -- AND DUYET = 0
		                    IF @MTDKID IS NOT NULL		
		                    BEGIN		
			                    EXEC SP_DeleteAllDatasFromForeignTable 'MTDK',@MTDKID
			                    DELETE FROM MTDK WHERE HVID = @MTDKID -- AND DUYET = 0
		                    END
	                    END
                    END	
                    IF @MODE = 'ADD' OR @MODE = 'EDIT'
                    BEGIN
                        -- KIỂM TRA XEM HV ĐÃ ĐĂNG KÝ LỚP NÀY CHƯA, NẾU CHƯA THÌ MỚI INSERT VÀO MTDK
                        IF NOT EXISTS (SELECT TOP 1 HVID FROM MTDK WHERE HVTVID = @HVTVID AND MALOP = @LOPHOCTHU)
                        BEGIN
			                    -- GET VALUES
			                    DECLARE @MACNHOC	NVARCHAR(128)
			                    DECLARE @PHONGHOC	NVARCHAR(128)
			                    DECLARE @MAGH		NVARCHAR(128)
			                    DECLARE @MAHV		NVARCHAR(128)

			                    SELECT	@NGAYDK = CASE WHEN @NGAYHOCTHU <= NGAYBDKHOA THEN NGAYBDKHOA ELSE @NGAYHOCTHU END, 
					                    @PHONGHOC = PHONGHOC,@MAGH = MAGIOHOC
			                    FROM	DMLOPHOC 
			                    WHERE	MALOP = @LOPHOCTHU	

			                    IF @NGAYSINH = ''
				                    SET @NGAYSINH = NULL
			                    SET @MACNHOC = @MACNDK			                    
			                    -- TẠO MAHV
                                --SET @MAHV = (SELECT dbo.[func_TaoMAHV](@MACNDK))
                    	        
                                -- TÍNH NGÀY NGHỈ (NGÀY KẾT THÚC HỌC THỬ)
                                SELECT TOP {3} NGAY INTO #TEMP FROM TEMPLICHHOC WHERE MALOP = @LOPHOCTHU AND NGAY >= @NGAYDK ORDER BY NGAY ASC 
                                SELECT @NGAYNGHI = MAX(NGAY) FROM #TEMP
                                DROP TABLE #TEMP
                                -- INSERT VÀO DANH SÁCH HỌC VIÊN ĐĂNG KÝ
                                INSERT INTO MTDK(HVID,MAHVTV,TenHV,HVTVID,NgayTN,NgayDK,MALOP,PhongHoc,MAGH,MaCNHoc,MaCNDK,NGAYSINH,ISNGHIHOC,NGAYNGHI,isDKL)
                                VALUES(NEWID(),@MAHVTV,@TENHV,@HVTVID,@NGAYDK,@NGAYDK,@LOPHOCTHU,@PHONGHOC,@MAGH,@MACNHOC,@MACNDK,@NGAYSINH,1,@NGAYNGHI,1)
                        END
                    END"
                , HVTVID, LopHT, LopHT_Ori, SoBuoiHT, CountDTDKLop, Mode, drMaster["MaCN"].ToString()
                , ReplaceDauNhayDon(drMaster["TenHV"].ToString()), drMaster["MaHV"], drMaster["NgayHT"], drMaster["NgaySinh"]);
            return sql;
        }
        private int GetHVTVID()
        {
            if (drMaster.RowState == DataRowState.Deleted)
                return (int)drMaster["HVTVID", DataRowVersion.Original];
            return drMaster["HVTVID"] != DBNull.Value ? (int)drMaster["HVTVID"] : -1;
        }
        private DataView GetDetailView()
        {
            DataView dv = new DataView(_data.DsData.Tables["DTDKLop"]);
            dv.RowStateFilter = DataViewRowState.CurrentRows | DataViewRowState.Deleted;
            dv.RowFilter = HVTVID == -1 ? "HVTVID IS NULL" : "HVTVID = " + HVTVID;
            return dv;
        }
        private DataView GetDetailViewCurrentRows()
        {
            DataView dv = new DataView(_data.DsData.Tables["DTDKLop"]);
            dv.RowStateFilter = DataViewRowState.CurrentRows;
            dv.RowFilter = HVTVID == -1 ? "HVTVID IS NULL" : "HVTVID = " + HVTVID;
            return dv;
        }
        private bool KiemTraDangKyLanDau(string MaLop_New, string MaLop_Old)
        {
            if (MaLop_New == MaLop_Old)
                return true;
            string sql = string.Format(@"
                --Delete From MTDK Where HVTVID = {0} And MaLop = '{1}' and isDKL = 1 --- isDKL:Học thử
                If Not Exists (Select HVID From MTDK Where HVTVID = {0} And MaLop = '{1}')                                                              
                    Select 0                 
                Else 
                Begin                
                    Update MTDK Set IsNghiHoc = 0, NgayNghi = NULL, isDKL = 0 Where HVTVID = {0} And MaLop = '{1}'
                    Select 1
                End"
                , HVTVID, MaLop_New);
            if ((int)_data.DbData.GetValue(sql) == 1)
                return false;
            return true;
        }
        private bool KiemTraThayDoiTruongLK()
        {
            if (drMaster["TruongLK"].ToString() != drMaster["TruongLK", DataRowVersion.Original].ToString())
            {
                string sql = string.Format(@"If Exists (Select HVID From MTDK Where HVTVID = {0}) Select 1 Else Select 0"
                                            , (int)drMaster["HVTVID"]);
                if ((int)db.GetValue(sql) == 1)
                {
                    _info.Result = false;
                    XtraMessageBox.Show("Không được thay đổi trường liên kết !\nVì học viên này đã đăng ký học với trường liên kết này", Config.GetValue("PackageName").ToString());
                }
            }
            return _info.Result;
        }
        private bool KiemTraDangKyLop(DataView dvDTDKLop,ref List<string> list_MTDKsWillDelete)
        {
            if (dvDTDKLop.Count > 0)
            {
                string Mess = "";
                string mess2 = "";
                string mess3 = "";
                string DTDKLopIDs = "";
                foreach (DataRowView drv in dvDTDKLop)
                {
                    if (drv.Row.RowState == DataRowState.Modified
                        || drv.Row.RowState == DataRowState.Deleted)
                    {                        
                        object s = drv.Row["MaLop", DataRowVersion.Original];
                        // nếu mã lớp or ngày đăng ký or cấp độ không thay đổi thì không cấn làm gì
                        if ((drv.Row.RowState == DataRowState.Modified
                            && drv.Row["NgayTN"].ToString() == drv.Row["NgayTN", DataRowVersion.Original].ToString()
                            && drv.Row["MaLop"].ToString() == drv.Row["MaLop", DataRowVersion.Original].ToString()
                            && drv.Row["NgayDK"].ToString() == drv.Row["NgayDK", DataRowVersion.Original].ToString()
                            && drv.Row["MaCD"].ToString() == drv.Row["MaCD", DataRowVersion.Original].ToString()))
                            continue;

                        //if (drv.Row["refMTDK", DataRowVersion.Original] != DBNull.Value)
                        //{
                            string DTDKLopID = drv.Row["ID", DataRowVersion.Original].ToString();
                            string MaLop = drv.Row["MaLop", DataRowVersion.Original].ToString();
                            string sql = string.Format(@"
                                    Declare @Duyet bit
                                    Select @Duyet = Duyet From MTDK Where refDTDKLop = '{0}'
                                    select @Duyet"
                                   , DTDKLopID);
                            object result = db.GetValue(sql);
                            if (result != null && result != DBNull.Value)
                            {
                                // MTDK đã duyệt
                                if ((bool)result)
                                    Mess += string.Format("- Dòng thu học phí có ngày thu :{0}.\n", ((DateTime)drv.Row["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy"));
                                // chưa duyệt
                                else
                                    list_MTDKsWillDelete.Add(drv.Row["refMTDK", DataRowVersion.Original].ToString());
                            }
                        //}
                        if (drv.Row.RowState == DataRowState.Deleted)
                        {
                            // kiểm tra DTDKLop đã có trong đề nghị khai giảng không
                            // nếu DNKG đã duyệt/trình duyệt thì thông báo không cho xóa
                            // nếu DNKG chưa duyệt thì thông báo lựa chọn có tiếp tục xóa hay không.
                            sql = string.Format(@"
                                If Exists (Select d.DTKGID From DTDNKG d inner join mtdnkg m on m.mtkgid = d.mtkgid Where DTDKID = '{0}' and m.TinhTrang <> N'Chưa duyệt')
	                                Select 1
                                Else Select 0", drv.Row["ID", DataRowVersion.Original]);
                            if ((int)_data.DbData.GetValue(sql) == 1)
                                mess2 += string.Format("- Dòng thu học phí có ngày thu :{0} \n", ((DateTime)drv.Row["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy"));


                            sql = string.Format(@"
                                If Exists (Select d.DTKGID From DTDNKG d inner join mtdnkg m on m.mtkgid = d.mtkgid Where DTDKID = '{0}' and m.TinhTrang = N'Chưa duyệt')
	                                Select 1
                                Else Select 0", drv.Row["ID", DataRowVersion.Original]);
                            if ((int)_data.DbData.GetValue(sql) == 1)
                            {
                                mess3 += string.Format("- Dòng thu học phí có ngày thu :{0} \n", ((DateTime)drv.Row["NgayTN", DataRowVersion.Original]).ToString("dd/MM/yyyy"));
                                DTDKLopIDs += " DTDKID = '" + drv.Row["ID", DataRowVersion.Original] + "' AND";
                            }
                        }
                    }
                }
                if (Mess != "")
                {
                    XtraMessageBox.Show("Học viên đã được duyệt, không cho phép sửa/xóa dữ liệu! \nNhững dòng dữ liệu sau thuộc tab 'Thu học phí' không được phép sửa/xóa : \n" + Mess, Config.GetValue("PackageName").ToString());
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
            }
            return _info.Result;
        }
        private bool KiemTraBeforePhieuThus(DataView dvDTDKLop)
        {
            if (dvDTDKLop.Count == 0)
                return _info.Result;
            // kiểm tra các phiếu MT11 or MT15 đã duyệt chưa , đã duyệt thì không được xóa/sửa.
            if (!CheckPhieus(dvDTDKLop))
                return _info.Result;
            string Mess = "";            
            foreach (DataRowView drv in dvDTDKLop)
            {

                if (drv.Row.RowState == DataRowState.Deleted)
                    continue;
                // nếu có nộp tiền
                if (drv.Row["TDaNop"] != DBNull.Value
                     && (decimal)drv.Row["TDaNop"] != 0)
                {
                    // phải chọn hình thức thanh toán
                    if (drv.Row["HTTT"] == DBNull.Value)
                        Mess += "- Vui lòng chọn hình thức thanh toán cho những đăng ký lớp đã có nộp tiền thuộc Tab 'Thu học phí' !\n";
                    // nếu có mua bàn tính thì phải chọn cấp độ
                    if ((bool)drv.Row["isMuaBT"] && drv.Row["MaCD"] == DBNull.Value)
                        Mess += "- Vui lòng chọn cấp độ cho những đăng ký lớp có mua giáo trình thuộc Tab 'Thu học phí' !";
                }
                if (Mess != "")
                {
                    XtraMessageBox.Show(Mess, Config.GetValue("PackageName").ToString());
                    _info.Result = false;
                    return _info.Result;
                }
            }
            return _info.Result;
        }
        private bool KiemTraHocThuInfos()
        {
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
            {
                if ((drMaster.RowState == DataRowState.Modified && drMaster["LopHocThu", DataRowVersion.Original] != DBNull.Value
                        && (drMaster["LopHocThu", DataRowVersion.Original].ToString() != drMaster["LopHocThu"].ToString()
                            || drMaster["LopHocThu"] == DBNull.Value))
                    || drMaster.RowState == DataRowState.Deleted)
                {
                    string sql = string.Format(@"
                        If Exists (Select HVID From MTDK Where HVTVID = {0} and MaLop = '{1}' and Duyet = 1 and isDKL = 1) Select 1 Else Select 0"
                        , drMaster["HVTVID", DataRowVersion.Original], drMaster["LopHocThu", DataRowVersion.Original]);
                    if ((int)_data.DbData.GetValue(sql) == 1)
                    {
                        _info.Result = false;
                        XtraMessageBox.Show(string.Format("Không được sửa/xóa thông tin học thử !\nHọc viên này đã được duyệt cho học thử lớp {0}", drMaster["LopHocThu", DataRowVersion.Original]), Config.GetValue("PackageName").ToString());
                        return _info.Result;
                    }
                }
            }
            
            if ((drMaster.RowState == DataRowState.Added || drMaster.RowState == DataRowState.Modified)
                &&drMaster["LopHocThu"] != DBNull.Value && drMaster["NgayHT"] == DBNull.Value)
            {
                _info.Result = false;
                XtraMessageBox.Show("Vui lòng điền thông tin [Ngày học thử]", Config.GetValue("PackageName").ToString());
                return _info.Result;
            }
            return _info.Result;
        }
        private string ReplaceDauNhayDon(string input)
        {
            return input.Replace("'", "''");
        }
        private void CreateMaHV()
        {
            if (drMaster.RowState == DataRowState.Added && drMaster["MaHV"] == DBNull.Value)
            {
                drMaster["MaHV"] = Guid.NewGuid().ToString().Substring(0, 16);
                // INSERT VÀO DANH MỤC KHÁCH HÀNG
                //                string sql = string.Format(@"
                //                        DECLARE @SDT NVARCHAR(128)
                //                        SET @SDT = '{4}'                        
                //                        IF @SDT = ''
                //                            SET @SDT = NULL
                //                        INSERT INTO DMKH(MAKH,TENKH,DIACHI,SDT,KHID,ISKH)
                //                        VALUES('{0}',N'{1}',N'{2}','{3}',{4},1)"
                //                    , drMaster["MaHV"], ReplaceDauNhayDon(drMaster["TenHV"].ToString())
                //                    , drMaster["DiaChi"], drMaster["DienThoai"], drMaster["HVTVID"]);
                //                _data.DbData.UpdateByNonQuery(sql);
            }
        }
        private void Update_DMHVTV_MTDK(DataRow dr, string xuly)
        {
            switch (xuly)
            {
                case "THEM":
                    if (Convert.ToInt32(dr["TienBL"]) > 0)
                    {
                        string sql2 = string.Format(@"select top 1 malop from dtdklop where hvtvid = {0}", dr["HVTVID"]);
                        db.EndMultiTrans();
                        object obj = db.GetValue(sql2);
                        if (obj != DBNull.Value)
                        {
                            //Cập nhật tiền bảo lưu trong mtdk
                            string sql = string.Format(@"update mtdk set blsotien = 0, isbl = 0 where malop = '{1}' and hvtvid = {0}", dr["HVTVID"], dr["MaLop"]);
                            db.UpdateByNonQuery(sql);
                            //cập nhật tình trạng học viên
                            InsertTinhTrang(dr["HVTVID"].ToString(), DateTime.Now, 4, Config.GetValue("Username").ToString(), true);
                        }
                    }
                    break;
                case "SUA":
                    break;
                case "XOA":
                    if (Convert.ToInt32(dr["TienBL", DataRowVersion.Original]) > 0)
                    {
                        string sql2 = string.Format(@"select top 1 malop from dtdklop where hvtvid = {0}", dr["HVTVID", DataRowVersion.Original]);
                        db.EndMultiTrans();
                        object obj = db.GetValue(sql2);
                        if (obj != DBNull.Value)
                        {
                            //Cập nhật tiền bảo lưu trong mtdk
                            string sql1 = string.Format(@"update mtdk set blsotien = {2},isbl = 1 where malop = '{1}' and hvtvid = {0}"
                                            , dr["HVTVID", DataRowVersion.Original], dr["MaLop", DataRowVersion.Original], dr["TienBL", DataRowVersion.Original]);
                            db.UpdateByNonQuery(sql1);
                            //cập nhật tình trạng học viên
                            InsertTinhTrang(dr["HVTVID", DataRowVersion.Original].ToString(), DateTime.Now, 2, Config.GetValue("Username").ToString(), false);
                        }
                    }
                    break;
            }
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
