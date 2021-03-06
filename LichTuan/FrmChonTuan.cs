using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.Globalization;
using CDTLib;
using CDTDatabase;

namespace LichTuan
{
    public partial class FrmChonTuan : DevExpress.XtraEditors.XtraForm
    {
        private Database _db = Database.NewDataDatabase();
        DateTimeFormatInfo dfi = new DateTimeFormatInfo();
        public DateTime NgayBD
        {
            get
            {
                return deTuNgay.DateTime;
            }
        }
        public DateTime NgayKT
        {
            get
            {
                return deDenNgay.DateTime;
            }
        }
        public FrmChonTuan()
        {
            InitializeComponent();
            dfi.LongDatePattern = "MM/dd/yyyy hh:mm:ss";
            dfi.ShortDatePattern = "MM/dd/yyyy";
            ceThuBay.Checked = true;
            ceCN.Checked = true;
        }

        private void FrmChonTuan_Load(object sender, EventArgs e)
        {
            seTuan.Value = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(DateTime.Today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday) + 1;
            seTuan_EditValueChanging(seTuan, new DevExpress.XtraEditors.Controls.ChangingEventArgs(null, seTuan.Value));
        }

        private void seTuan_EditValueChanging(object sender, DevExpress.XtraEditors.Controls.ChangingEventArgs e)
        {
            string s = e.NewValue.ToString();
            string w = s.EndsWith(".") || s.EndsWith(",") ? s.Substring(0, s.Length - 1) : s;
            string year = Config.GetValue("NamLamViec").ToString();
            DateTime dtdn = new DateTime(Int32.Parse(year), 1, 1);
            DateTime dt = dtdn.AddDays((Double.Parse(w) - 1) * 7);
            DateTime dts = dt;
            while (dts.DayOfWeek != DayOfWeek.Monday)
                dts = dts.AddDays(-1);
            int days = ceCN.Checked ? 6 : (ceThuBay.Checked ? 5 : 4);
            DateTime dte = dts.AddDays(days);
            deTuNgay.DateTime = dts;
            deDenNgay.DateTime = dte;
        }

        private void ceThuBay_CheckedChanged(object sender, EventArgs e)
        {
            if (ceThuBay.Checked)
                deDenNgay.DateTime = deDenNgay.DateTime.AddDays(1);
            else
                deDenNgay.DateTime = deDenNgay.DateTime.AddDays(-1);
            if (!ceThuBay.Checked)
                ceCN.Checked = false;
        }

        private void ceCN_CheckedChanged(object sender, EventArgs e)
        {
            if (ceCN.Checked)
                ceThuBay.Checked = true;
            if (ceCN.Checked)
                deDenNgay.DateTime = deDenNgay.DateTime.AddDays(1);
            else
                deDenNgay.DateTime = deDenNgay.DateTime.AddDays(-1);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (rgTuyChon.SelectedIndex == 1)
            {
                XoaLich();
                ChepLich();
            }
            if (rgTuyChon.SelectedIndex == 2)
            {
                XoaLich();
                TaoLich(false, false);
            }
            if (rgTuyChon.SelectedIndex == 3)
                TaoLich(true, true);
            if (rgTuyChon.SelectedIndex == 0)
            {
                object o = _db.GetValue(string.Format("select count(MaLop) from ChamCongGV where Ngay between '{0}' and '{1}' and MaLop in (select MaLop from DMLopHoc where MaCN = '{2}')", deTuNgay.DateTime, deDenNgay.DateTime, Config.GetValue("MaCN")));
                if (o != null && o.ToString() != "")
                    if (Int32.Parse(o.ToString()) == 0)
                    {
                        XtraMessageBox.Show("Tuần này chưa được tạo lịch, cần tạo lịch mới trước", Config.GetValue("PackageName").ToString());
                        return;
                    }
            }
            this.DialogResult = DialogResult.OK;
        }

        private decimal TinhSoGio(DateTime bd, DateTime kt, string malop, string maca)
        {
            try
            {
                decimal sp = kt.Hour + kt.Minute / 60 - (bd.Hour + bd.Minute / 60);
                return sp;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Lỗi khi tính số giờ dạy\n" + ex.Message, Config.GetValue("PackageName").ToString());
                return -1;
            }
        }

        //lấy danh sách tất cả các lớp chưa kết thúc
        private DataTable LayDSLop(bool ThemLop)
        {
            string sql = @"select lh.MaLop, lh.PhongHoc, lh.MaGioHoc, NgayBDKhoa = isnull(gv.NgayBD,lh.NgayBDKhoa), NgayKTKhoa = isnull(gv.NgayKT,lh.NgayKTKhoa), lh.BDNghi, lh.KTNghi, nv.ID as GVID, lh.MaCa, ct.Value as Thu, ca.TGBD, ca.TGKT, gv.* from 
                DMLopHoc lh inner join DMNgayGioHoc gh on lh.MaGioHoc = gh.MaGioHoc inner join CTGioHoc ct on lh.MaGioHoc = ct.MaGioHoc inner join DMCa ca on lh.MaCa = ca.MaCa inner join GVPhuTrach gv on gv.MaLop = lh.MaLop left join DMNVien nv on gv.MaGV = nv.MaNV
                where lh.isKT = 0 and MaCN ='" + Config.GetValue("MaCN").ToString() + "'";
			if (ThemLop)
                sql += string.Format(" and lh.MaLop not in (select MaLop from ChamCongGV where Ngay between '{0}' and '{1}')", deTuNgay.DateTime, deDenNgay.DateTime);
            DataTable dt = _db.GetDataTable(sql);
            return dt;
        }

        private void XoaLich()
        {
            string sql = "delete from ChamCongGV where Ngay between '{0}' and '{1}' and MaLop in (select MaLop from DMLopHoc where MaCN = '{2}')";
            _db.UpdateByNonQuery(String.Format(sql, deTuNgay.DateTime, deDenNgay.DateTime, Config.GetValue("MaCN")));
        }

        private void ChepLich()
        {
            //copy dữ liệu
            string sql = @"INSERT INTO ChamCongGV([GVID],[MaLop],[Ngay],[TinhTrang],[GVDayThay],[MaGio],[Thang],[Nam],[GhiChuCC],[TGBD],[TGKT],[Phong],[MaCa],[Tiet],[LC])
                    SELECT [GVID],[MaLop],dateadd(d, 7, [Ngay]),[TinhTrang],[GVDayThay],[MaGio],[Thang],[Nam],[GhiChuCC],[TGBD],[TGKT],[Phong],[MaCa],[Tiet],[LC]
                    FROM [ChamCongGV]
                    WHERE Ngay BETWEEN dateadd(d, -7, '{0}') and dateadd(d, -7, '{1}')
                        AND MaLop in (select MaLop from DMLopHoc where NgayKTKhoa >= dateadd(d, 7, Ngay) and isKT = 0 and MaCN = '{2}')";
            _db.UpdateByNonQuery(String.Format(sql, deTuNgay.DateTime, deDenNgay.DateTime, Config.GetValue("MaCN")));
            //cập nhật lại tháng và năm theo kiểu hikari (từ ngày 26 tháng này thì tính sang tháng sau)
            string s6 = @"update chamconggv set
                thang = case when day(ngay) < 26 then month(ngay)
		                else
			                case when month(ngay) < 12 then month(ngay) + 1
			                else 1 end
		                end,
                nam = case when day(ngay) >= 26 and month(ngay) = 12 then year(ngay) + 1
		                else year(ngay) end
                where ngay between '{0}' and '{1}'";
            _db.UpdateByNonQuery(String.Format(s6, deTuNgay.DateTime, deDenNgay.DateTime));
            //bổ sung thêm lớp mới nếu có
            TaoLich(true, false);
        }

        //nạp dữ liệu vào ChamCongGV
        private void TaoLich(bool ThemLop, bool ThemBuoi)
        {
            DataTable dtLN = LayLichNghi();
            DataTable dtLop = LayDSLop(ThemLop && !ThemBuoi);
            DateTime dtBD = deTuNgay.DateTime;
            DateTime dtKT = deDenNgay.DateTime;
            string sql = @"insert into ChamCongGV(MaLop, GVID, MaGio, Ngay, Thang, Nam, TGBD, TGKT, Phong, MaCa, Tiet)
                values(@MaLop, @GVID, @MaGio, @Ngay, @Thang, @Nam, @TGBD, @TGKT, @Phong, @MaCa, @Tiet)";
            if (ThemLop && ThemBuoi)
                sql = "if not exists (select * from ChamCongGV where MaLop = @MaLop and Ngay = @Ngay and MaCa = @MaCa) " + sql;
            string[] paras = new string[] {"MaLop", "GVID", "MaGio", "Ngay", "Thang", "Nam", "TGBD", "TGKT", "Phong", "MaCa", "Tiet"};
            bool check = true;
            _db.BeginMultiTrans();
            foreach (DataRow drLop in dtLop.Rows)   //duyệt qua từng lớp trong danh sách để tạo lịch dạy cho từng lớp
            {
                DateTime dtBDDay = DateTime.Parse(drLop["NgayBDKhoa"].ToString());
                DateTime dtKTLop = DateTime.Parse(drLop["NgayKTKhoa"].ToString());
                if (dtKT < dtBDDay || dtKTLop < dtBD)
                    continue;
                DateTime dtBDTinh = dtBDDay > dtBD ? dtBDDay : dtBD;  //kiểm tra giới hạn thời gian để tạo lịch
                DateTime dtKTTinh = dtKTLop < dtKT ? dtKTLop : dtKT;
                DateTime dtNgayDay = LayNgay(dtBDTinh, dtKTTinh, dtLN, drLop, drLop["Thu"].ToString());
                if (dtNgayDay == DateTime.MinValue)  //khong co lich
                    continue;
                //int month = (dtNgayDay.Day < 26) ? dtNgayDay.Month : dtNgayDay.Month + 1;
                //int year = dtNgayDay.Year;
                //if (month == 13)
                //{
                //    month = 1;
                //    year = dtNgayDay.Year + 1;
                //}
                if (drLop["TGBD"].ToString() == "" || drLop["TGKT"].ToString() == "")
                {
                    check = false;
                    continue;
                }
                decimal tiet = TinhSoGio(DateTime.Parse(drLop["TGBD"].ToString()), DateTime.Parse(drLop["TGKT"].ToString()), drLop["MaLop"].ToString(), drLop["MaCa"].ToString());
                object[] values = new object[] {drLop["MaLop"], drLop["GVID"], drLop["MaGioHoc"], dtNgayDay, dtNgayDay.Month, 
                    dtNgayDay.Year, drLop["TGBD"], drLop["TGKT"], drLop["PhongHoc"], drLop["MaCa"], tiet};
                if (!_db.UpdateDatabyPara(sql,paras, values))
                    break;
            }
            if (!_db.HasErrors)
                _db.EndMultiTrans();
            if (check == false)
                XtraMessageBox.Show("Một số lớp chưa thiết lập đủ thông tin về thời gian học",
                    Config.GetValue("PackageName").ToString());
        }

        private DataTable LayLichNghi()
        {
            string sql = @"select tl.* from TLNgayNghiLop tl inner join DMLopHoc lh on tl.MaLop = lh.MaLop
                where lh.isKT = 0 and lh.MaCN = '" + Config.GetValue("MaCN").ToString() + "'";
            return (_db.GetDataTable(sql));
        }

        private bool TrungLichNghi(DateTime ngay, DataView dvLN)
        {
            foreach (DataRowView drv in dvLN)
                if (ngay >= DateTime.Parse(drv["NgayNghi"].ToString(), dfi)
                    && ngay <= DateTime.Parse(drv["DenNgay"].ToString(), dfi))
                    return true;
            return false;
        }

        private DateTime LayNgay(DateTime ngayBD, DateTime ngayKT, DataTable dtLichNghi, DataRow drLop, string thu)
        {
            DateTime dt = DateTime.MinValue;
            DayOfWeek dow = DayOfWeek.Sunday;   //bắt buộc phải có giá trị khởi tạo, vì vậy phải thêm biến check để kiểm tra
            bool check = false;
            switch (thu)
            {
                case "2":
                    if (bool.Parse(drLop["Mon"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Monday;
                    }
                    break;
                case "3":
                    if (bool.Parse(drLop["Tue"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Tuesday;
                    }
                    break;
                case "4":
                    if (bool.Parse(drLop["Wed"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Wednesday;
                    }
                    break;
                case "5":
                    if (bool.Parse(drLop["Thur"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Thursday;
                    }
                    break;
                case "6":
                    if (bool.Parse(drLop["Fri"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Friday;
                    }
                    break;
                case "7":
                    if (bool.Parse(drLop["Sat"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Saturday;
                    }
                    break;
                default:
                    if (bool.Parse(drLop["Sun"].ToString()))
                    {
                        check = true;
                        dow = DayOfWeek.Sunday;
                    }
                    break;
            }
            if (!check)
                return dt;
            //duyệt qua lịch học, so sánh với lịch nghỉ và lịch dạy để lấy ngày
            string ml = drLop["MaLop"].ToString();
            dtLichNghi.DefaultView.RowFilter = "MaLop = '" + ml + "'";
            for (DateTime dtp = ngayBD; dtp <= ngayKT; dtp = dtp.AddDays(1))
            {
                if (TrungLichNghi(dtp, dtLichNghi.DefaultView))
                    continue;
                if (dtp.DayOfWeek == dow)
                {
                    dt = dtp;
                    break;
                }
            }
            return dt;
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            FrmMauGV frm = new FrmMauGV();
            frm.ShowDialog();
        }
    }
}