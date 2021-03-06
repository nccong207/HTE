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
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Globalization;

namespace KiemTraTL
{
    public class KiemTraTL : ICData
    {
        private InfoCustomData _info;
        private DataCustomData _data;
        Database db = Database.NewDataDatabase();
        DateTimeFormatInfo dfi = new DateTimeFormatInfo();

        DataRow drMaster;
        //DataRow drGVPhuTrach; //HVKHOI

        #region ICData Members

        public KiemTraTL()
        {
            _info = new InfoCustomData(IDataType.MasterDetailDt);
            dfi.LongDatePattern = "MM/dd/yyyy hh:mm:ss";
            dfi.ShortDatePattern = "MM/dd/yyyy";
        }
        
        public InfoCustomData Info
        {
            get { return _info; }
        }
        public DataCustomData Data
        {
            set { _data = value; }
        }

        public void ExecuteAfter()
        {
            if (_data.CurMasterIndex < 0)
                return;
            DataRow drLop = _data.DsDataCopy.Tables[0].Rows[_data.CurMasterIndex];
            drMaster = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];
            string malop = drLop.RowState == DataRowState.Added ? drLop["MaLop", DataRowVersion.Current].ToString() : drLop["MaLop", DataRowVersion.Original].ToString();
            //xoa lich cu truoc
            _data.DbData.UpdateByNonQuery("delete from TempLichHoc where MaLop = '" + malop + "'");
            // Insert TempLichHoc
            if (drLop.RowState != DataRowState.Deleted)
            {
                // Note : phải lấy Value để so sánh không phải Thu
                //lay them thong tin ngay gio hoc
                string s = @"select gh.MaGioHoc, gh.MaCa, ct.Value, ct.TGBD, ct.TGKT
                            from    DMNgayGioHoc gh 
                                    inner join CTGioHoc ct on gh.MaGioHoc = ct.MaGioHoc
                            where   gh.MaGioHoc = '" + drLop["MaGioHoc"].ToString() + "'";
                DataTable dtLH = _data.DbData.GetDataTable(s);

                DateTime dtBD = DateTime.Parse(drLop["NgayBDKhoa"].ToString());
                DateTime dtKT = DateTime.Parse(drLop["NgayKTKhoa"].ToString());
                string sql = @"insert into templichhoc(MaLop, Ngay, MaGio, MaCa, TGBD, TGKT)
                                values(@MaLop, @Ngay, @MaGio, @MaCa, @TGBD, @TGKT)";
                string[] paras = new string[] { "MaLop", "Ngay", "MaGio", "MaCa", "TGBD", "TGKT" };

                foreach (DataRow dr in dtLH.Rows)
                {
                    DataTable dtNgayDay = LayNgay(dtBD, dtKT, drLop, dr["Value"].ToString());
                    foreach (DataRow drvNgay in dtNgayDay.Rows)
                    {
                        object[] values = new object[] { drLop["MaLop"], drvNgay["NgayDay"], dr["MaGioHoc"], dr["MaCa"], dr["TGBD"], dr["TGKT"] };
                        _data.DbData.UpdateDatabyPara(sql, paras, values);
                    }
                }
            }

            //Duyệt, bỏ duyệt, xóa
            DuyetDSHV();
            //END HVKHOI
        }
        
        public void ExecuteBefore()
        {
            if (_data.CurMasterIndex < 0)
                return;
            drMaster = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];

            KiemTra();
            if (_info.Result == true)
                KiemTraNN();
            if (_info.Result == true)
                KiemTraDoiLich();
            //HVKHOI 
            //Kiểm tra Trùng MaLop trước khi lưu, nếu MaLop đã tồn tại thì đổi sang mã lớp mới
            if (drMaster != null && drMaster.RowState == DataRowState.Added)
                KiemTraMaLop();
            //Kiểm tra thông tin GVCN, GVPT: ktra thông tin giáo viên
            if (drMaster.RowState != DataRowState.Deleted)
            {
                if (_data.DsData.Tables.Count > 1 && drMaster != null)
                {
                    //DataRow[] dr = _data.DsData.Tables[1].Select(string.Format("MaLop = '{0}'", drMaster["MaLop"]));
                    
                    DataView dv = new DataView(_data.DsData.Tables[1]);
                    dv.RowFilter = string.Format("MaLop = '{0}'", drMaster["MaLop"]);
                    KiemTraThongTinGV(dv);
                    dv.RowFilter = "";
                }
            }
            // Ktra nếu bị xóa ở MTDK
            KhongXoaHV();
            CapNhatSSLop();
            //END HVKHOI
            //Khi sửa ngày kết thúc khóa học thì insert vào DTQTNghi
            if (drMaster.RowState == DataRowState.Modified)
            {
                if (drMaster["NgayKTKhoa", DataRowVersion.Original].ToString() != drMaster["NgayKTKhoa", DataRowVersion.Current].ToString())
                {
                    DataTable dt = _data.DsData.Tables[8];// Vitri 7: bảng DTQTNghi
                    DataRow dr = dt.NewRow();
                    dr["Ngay"] = DateTime.Today;
                    dr["MaLop"] = drMaster["MaLop"];
                    dr["MaNV"] = Config.GetValue("UserName").ToString();
                    dr["DienGiai"] = string.Format("Ngày nghỉ: chuyển từ [{0}] sang [{1}]"
                                , ((DateTime)drMaster["NgayKTKhoa", DataRowVersion.Current]).ToString("dd/MM/yyyy")
                                , ((DateTime)drMaster["NgayKTKhoa", DataRowVersion.Original]).ToString("dd/MM/yyyy"));
                    dt.Rows.Add(dr);
                }
            }
        }

        #region Tạo lịch tạm của lớp

        private bool DoiLich(DataRow drLop)
        {
            string oMaLop = drLop["MaLop", DataRowVersion.Original].ToString();
            string oMaGioHoc = drLop["MaGioHoc", DataRowVersion.Original].ToString();
            string oNgayBDKhoa = drLop["NgayBDKhoa", DataRowVersion.Original].ToString();
            string oNgayKTKhoa = drLop["NgayKTKhoa", DataRowVersion.Original].ToString();

            string MaLop = drLop["MaLop", DataRowVersion.Current].ToString();
            string MaGioHoc = drLop["MaGioHoc", DataRowVersion.Current].ToString();
            string NgayBDKhoa = drLop["NgayBDKhoa", DataRowVersion.Current].ToString();
            string NgayKTKhoa = drLop["NgayKTKhoa", DataRowVersion.Current].ToString();

            if (oMaLop == MaLop && oMaGioHoc == MaGioHoc && oNgayBDKhoa == NgayBDKhoa && oNgayKTKhoa == NgayKTKhoa)
                return false;
            return true;
        }

        private bool TrungLichNghi(DateTime ngay, DataView dvLN)
        {
            foreach (DataRowView drv in dvLN)
                if (ngay >= DateTime.Parse(drv["NgayNghi"].ToString(), dfi)
                    && ngay <= DateTime.Parse(drv["DenNgay"].ToString(), dfi))
                    return true;
            return false;
        }

        private DataTable LayNgay(DateTime ngayBD, DateTime ngayKT, DataRow drLop, string value)
        {
            DataTable dtLich = new DataTable(); // Danh sach cac ngay day cua lop 
            DataColumn colNgay = new DataColumn("NgayDay", typeof(DateTime));
            dtLich.Columns.Add(colNgay);
            DayOfWeek dow;
            switch (value)
            {
                case "2":
                        dow = DayOfWeek.Monday;
                    break;
                case "3":
                        dow = DayOfWeek.Tuesday;
                    break;
                case "4":
                        dow = DayOfWeek.Wednesday;
                    break;
                case "5":
                        dow = DayOfWeek.Thursday;
                    break;
                case "6":
                        dow = DayOfWeek.Friday;
                    break;
                case "7":
                        dow = DayOfWeek.Saturday;
                    break;
                default:
                        dow = DayOfWeek.Sunday;
                    break;
            }
            //duyệt qua lịch học, so sánh với lịch nghỉ và lịch dạy để lấy ngày
            string ml = drLop["MaLop"].ToString();
            DataView dvLN = new DataView(_data.DsData.Tables[3]);
            dvLN.RowFilter = "MaLop = '" + ml + "'";
            for (DateTime dtp = ngayBD; dtp <= ngayKT; dtp = dtp.AddDays(1))
            {
                if (TrungLichNghi(dtp, dvLN))
                    continue;
                if (dtp.DayOfWeek == dow)
                {
                    DataRow dr = dtLich.NewRow();
                    dr["NgayDay"] = dtp;
                    dtLich.Rows.Add(dr);
                }
            }
            return dtLich;
        }
        #endregion

        // Ktra thiết lập lịch dạy của giáo viên
        void KiemTra()
        {
            if (_data.CurMasterIndex < 0)
                return;
            Database db = Database.NewDataDatabase();
            DataRow drMaster = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];
            if (drMaster.RowState == DataRowState.Deleted)
                return;
            string sql = @"select ct.Value from dmngaygiohoc gh inner join CTGioHoc ct on ct.MaGioHoc = gh.MaGioHoc
                            where gh.MaGioHoc ='" + drMaster["MaGioHoc"].ToString() + "'";
            DataTable dt = db.GetDataTable(sql);
            DataView dvThu = new DataView(dt);
            DataView dv = new DataView(_data.DsData.Tables[1]);
            dv.RowFilter = " MaLop = '" + drMaster["MaLop"].ToString() + "'";
            if (dv.Count == 0 || dvThu.Count == 0)
                return;

            bool flag = KiemTraThu(dv, dvThu);
            if (flag)
            {
                XtraMessageBox.Show("Thiết lập lịch dạy (ngày trong tuần) của giáo viên, chưa khớp với lịch học của lớp!");
                _info.Result = false;
            }
        }

        bool KiemTraThu(DataView dv, DataView dvThu)
        {
            bool flag = false;
            foreach (DataRowView drv in dv)
            {
                if (bool.Parse(drv["Sun"].ToString()))
                {
                    dvThu.RowFilter = "Value = 1";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Mon"].ToString()))
                {
                    dvThu.RowFilter = "Value = 2";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Tue"].ToString()))
                {
                    dvThu.RowFilter = "Value = 3";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Wed"].ToString()))
                {
                    dvThu.RowFilter = "Value = 4";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Thur"].ToString()))
                {
                    dvThu.RowFilter = "Value = 5";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Fri"].ToString()))
                {
                    dvThu.RowFilter = "Value = 6";
                    if (dvThu.Count == 0)
                        return true;
                }
                if (bool.Parse(drv["Sat"].ToString()))
                {
                    dvThu.RowFilter = "Value = 7";
                    if (dvThu.Count == 0)
                        return true;
                }
                foreach (DataRowView dr in dv)
                {
                    if (!bool.Parse(drv["Sun"].ToString()) && !bool.Parse(drv["Mon"].ToString()) && !bool.Parse(drv["Tue"].ToString()) && !bool.Parse(drv["Wed"].ToString()) && !bool.Parse(drv["Thur"].ToString()) && !bool.Parse(drv["Fri"].ToString()) && !bool.Parse(drv["Sat"].ToString()))
                        return true;
                }
            }
            return flag;
        }

        // Ktra ngày nghỉ
        public void KiemTraNN()
        {
            //Database db = Database.NewDataDatabase();
            //DataRow drNN = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];
            //if (drNN.RowState == DataRowState.Deleted)
            //    return;
            //DataView dv = new DataView(_data.DsData.Tables[3]);
            //dv.RowFilter = "MaLop ='" + drNN["MaLop"].ToString() + "'";
            //if (dv.Count == 0)
            //{
            //    DialogResult result = XtraMessageBox.Show("Lớp chưa có ngày nghỉ !\nBạn có muốn tiếp tục lưu", Config.GetValue("PackageName").ToString(), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            //    if (result == DialogResult.No)
            //        _info.Result = false;
            //    else
            //        _info.Result = true;
            //}
        }

        // Ko đc đổi lịch học
        private void KiemTraDoiLich()
        {
            if (_data.CurMasterIndex < 0)
                return;
            if (drMaster.RowState == DataRowState.Added || drMaster.RowState == DataRowState.Deleted)
                return;
            if ((drMaster["MaGioHoc", DataRowVersion.Original].ToString() != drMaster["MaGioHoc", DataRowVersion.Current].ToString())
                || (drMaster["SoBuoi", DataRowVersion.Original].ToString() != drMaster["SoBuoi", DataRowVersion.Current].ToString())
                || (drMaster["NgayBDKhoa", DataRowVersion.Original].ToString() != drMaster["NgayBDKhoa", DataRowVersion.Current].ToString()))
                //|| (drMaster["NgayKTKhoa", DataRowVersion.Original].ToString() != drMaster["NgayKTKhoa", DataRowVersion.Current].ToString()))
            {
                // Ktra trong table MTDK
                if (_data.DsData.Tables["MTDK"].Select(string.Format("MaLop = '{0}'", drMaster["MaLop"])).Length > 0)
                {
                    XtraMessageBox.Show("Không thể thay đổi lịch học của lớp đã có danh sách học viên đăng ký", Config.GetValue("PackageName").ToString());
                    _info.Result = false;
                }
            }
        }
         
        private void KhongXoaHV()
        {
            // Không cho xóa danh sách học viên trong MTDK
            DataTable dtMTDK = _data.DsData.Tables[2].GetChanges(DataRowState.Deleted);
            if (dtMTDK != null && dtMTDK.Rows.Count > 0)
            {
                XtraMessageBox.Show("Không được xóa danh sách học viên đăng ký.",
                    Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return;
            }
        }
        private void CapNhatSSLop()
        {
            using (DataTable dtMTDK = _data.DsData.Tables[2].GetChanges(DataRowState.Modified))
            {
                if (dtMTDK != null && dtMTDK.Rows.Count > 0)
                {
                    //cập nhật sỉ số lớp
                    DataRow[] drs = _data.DsData.Tables[2].Select(string.Format("MaLop = '{0}' and Duyet = 1 and IsNghiHoc = 0 and IsBL = 0", drMaster["MaLop"]));
                    drMaster["Siso"] = drs.Length;
                }
            }
        }

        /* Cập nhật DMHVTV, lưu thông tin người duyệt
             * Duyệt cập nhật tình trạng Đã xếp lớp
             * Bỏ duyệt tình trạng = Đang chờ xếp lớp*/
        private void DuyetDSHV()
        {
            //Khi nhấn Duyệt/Bỏ duyệt thì ICC xử lý và insert thông tin
            DataTable dtMTDK = _data.DsData.Tables[2].GetChanges(DataRowState.Modified);
            if (dtMTDK != null && dtMTDK.Rows.Count > 0)
            {
                string sql = "";
                foreach (DataRow drMTDK in _data.DsData.Tables[2].Rows)// Table danh sách học viên
                {
                    if (drMTDK["Duyet", DataRowVersion.Current].ToString() != "" &&
                        drMTDK["Duyet", DataRowVersion.Current].ToString().ToUpper() == "TRUE")
                    {
                        sql += string.Format(@" UPDATE DMHVTV SET TinhTrang = 4 WHERE HVTVID = '{0}' ;
                                INSERT INTO DTTinhTrang (HVID, Ngay, TinhTrangID, MoTa, MaNV)
                                values ('{0}', getdate(), 4, N'Chuyển tình trạng: Đang chờ xếp lớp sang Đã xếp lớp', '{1}') ; ",
                         drMTDK["HVTVID"], Config.GetValue("UserName"));
                    }
                    else
                    {
                        sql += string.Format(@" UPDATE DMHVTV SET TinhTrang = 6 WHERE HVTVID = '{0}' ;", drMTDK["HVTVID"]);
                    }
                }
                if (!string.IsNullOrEmpty(sql))
                    db.UpdateByNonQuery(sql);
            }
        }

        private void KiemTraThongTinGV(DataView dv)
        {
            //Bắt buộc nhật thông tin gvcn
            //Cập nhật thông tin giáo viên chủ nhiệm lên master
            dv.RowFilter = string.Format("ThuocGV = 'GV chủ nhiệm' AND MaLop = '{0}'", drMaster["MaLop"]);
            dv.Sort = "NgayBD DESC";
            if (dv.Count > 0)
                drMaster["GVPT"] = dv[0]["MaGV"];
            else
            {
                XtraMessageBox.Show("Cần nhập giáo viên chủ nhiệm trong danh sách giáo viên phụ trách."
                    , Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return;
            }
            // Cập nhật giáo viên trợ giảng lên master
            dv.RowFilter = string.Format("ThuocGV = 'GV trợ giảng' AND MaLop = '{0}'", drMaster["MaLop"]);
            dv.Sort = "NgayBD DESC";
            if (dv.Count > 0)
                drMaster["GVTG"] = dv[0]["MaGV"];
            dv.RowFilter = "";
        }

        private void KiemTraMaLop()
        {
            string MaLop = drMaster["MaLop"].ToString();
            using (DataTable tmp_MaLop = db.GetDataTable(string.Format("SELECT MaLop FROM DMLopHoc WHERE MaLop = '{0}'", MaLop)))
            {
                if (tmp_MaLop.Rows.Count >= 0)
                    CreateMaLop();
            }
        }

        private void CreateMaLop()
        {
            string MaLopNew = "";
            // Malop = MaCN + STT
            string sql = string.Format(@"
                                SELECT TOP 1
                                    MaLop,
	                                ISNULL(replace(MaLop, '{0}', ''), 1) [STT] -- Lấy số thứ tự lớp nhất
                                FROM DMLopHoc
                                WHERE MaNLop = '{0}' AND ISNUMERIC(replace(MaLop, '{0}', '')) = 1
                            ORDER BY cast(replace(MaLop, '{0}', '') as int) desc  ", drMaster["MaNLop"]);
            using (DataTable dt = db.GetDataTable(sql))
            {
                if (dt == null || dt.Rows.Count == 0)
                    MaLopNew = drMaster["MaNLop"].ToString() + "1";
                else
                {
                    Int32 _stt = Convert.ToInt32(dt.Rows[0]["STT"].ToString()) + 1;
                    MaLopNew = drMaster["MaNLop"].ToString() + _stt.ToString();
                }
            }
            if (!string.IsNullOrEmpty(MaLopNew))
                drMaster["MaLop"] = MaLopNew;

        }

        #region Ko dung
        //private DateTime TinhNgayKT(string MaLop, DateTime NgayBD, int SoBuoic)
        //{
        //    DataTable dt = db.GetDataTable(string.Format("exec TinhNgayKT '{0}','{1}', '{2}'", SoBuoic, NgayBD, MaLop));
        //    // tính theo số buổi được học của học viên khi đóng tiền
        //    if (dt.Rows.Count == 0)
        //    {
        //        return DateTime.MinValue;
        //    }
        //    DateTime NgayKT = DateTime.Parse(dt.Rows[0]["NgayKT"].ToString());

        //    return NgayKT;
        //}

//        public void InsertCSHVTV(DataRowView drv)
//        {
//            string sql = @"INSERT INTO CSHVTV(HTChamSoc,NoiDung,KetQua,GhiChu,NgayHT,NVTV,RefDKID,RefMaLop)
//                            VALUES (@HTChamSoc,@NoiDung,@KetQua,@GhiChu,@NgayHT,@NVTV,@RefDKID,@RefMaLop)";
//            string[] para = new string[] { "HTChamSoc", "NoiDung", "KetQua", "GhiChu", "NgayHT", "NVTV","RefDKID" };
//            object[] value = new Object[] { drv.Row["HinhThuc"], drv.Row["NoiDung"], drv.Row["KetQua"], drv.Row["GhiChu"], drv.Row["NgayHT"], drv.Row["NVTV"],drv.Row["DKID",drv.Row["MaLop"] };
//            db.UpdateDatabyPara(sql, para, value);
//        }

//        public void UpdateCSHVTV(DataRowView drv)
//        {
//            string sql = @"UPDATE CSHVTV 
//                           SET HTChamSoc=@HTChamSoc,NoiDung=@NoiDung,KetQua=@KetQua,GhiChu=@GhiChu,NgayHT=@NgayHT,NVTV=@NVTV
//                           WHERE RefDKID=@RefDKID";
//            string[] para = new string[] { "HTChamSoc", "NoiDung", "KetQua", "GhiChu", "NgayHT", "NVTV", "RefDKID"};
//            object[] value = new Object[] { drv.Row["HinhThuc"], drv.Row["NoiDung"], drv.Row["KetQua"], drv.Row["GhiChu"], drv.Row["NgayHT"], drv.Row["NVTV"],drv.Row["DKID"] };
//        }

//        public void DeleteCSHVTV(string maLop)
//        {
//            string sql = @"DELETE FROM CSHVTV WHERE RefMaLop = '" + maLop.ToString() + "'";
//            db.UpdateByNonQuery(sql);
//        }

//        public void InsertCSHVTV(string maLop)
//        {
//            //_data.DbData.EndMultiTrans();
//            string sql = string.Format(@"INSERT INTO CSHVTV(HTChamSoc,HVTVID,NoiDung,KetQua,GhiChu,NgayHT,NVTV,RefDKID,RefMaLop) 
//                                        SELECT dk.HinhThuc,dk.HVTVID,dk.NoiDung,dk.KetQua,dk.GhiChu,dk.NgayHT,dk.NVTV,dk.DKID,dk.MaLop
//                                        FROM CSHVDK dk 
//                                        WHERE dk.MaLop='{0}'",maLop);
//            db.UpdateByNonQuery(sql);
        //        }
        #endregion

        #endregion
    }
}
