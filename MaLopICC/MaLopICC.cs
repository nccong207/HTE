using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using CDTControl;
using CDTDatabase;
using CDTLib;
using Plugins;
using DevExpress.XtraEditors;
using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using DevExpress.XtraLayout;
using System.Drawing;
using DevExpress.XtraEditors.Repository;

namespace MaLopICC
{
    public class MaLopICC : ICControl
    {
        DataCustomFormControl data;
        InfoCustomControl info = new InfoCustomControl(IDataType.MasterDetailDt);
        DataRow drMaster;
        Database db = Database.NewDataDatabase();
        GridView gvDiemDanh, gvMTDK;
        XtraTabControl tcMain;
        LayoutControl lcMain;
        DateEdit dateNgayBD;
        DataTable dtCTGioHoc = null;
        DataTable dtNgayNghi = new DataTable();
        DateEdit dateNgayKT;
        GridView gvNgayNghi;
        
        int thang, nam;
        #region ICControl Members

        public void AddEvent()
        {
            //AnThongTin();
            gvDiemDanh = (data.FrmMain.Controls.Find("wDiemDanh", true)[0] as GridControl).MainView as GridView;
            //Fix: Khi duyệt/bỏ duyệt, cập nhật thông tin người duyệt vào tab MTDK
            gvNgayNghi = (data.FrmMain.Controls.Find("TLNgayNghiLop", true)[0] as GridControl).MainView as GridView;
            gvMTDK = (data.FrmMain.Controls.Find("MTDK", true)[0] as GridControl).MainView as GridView;
            tcMain = data.FrmMain.Controls.Find("tcMain", true)[0] as XtraTabControl;
            lcMain = data.FrmMain.Controls.Find("lcMain", true)[0] as LayoutControl;
            dateNgayBD = data.FrmMain.Controls.Find("NgayBDKhoa", true)[0] as DateEdit;
            dateNgayKT = data.FrmMain.Controls.Find("NgayKTKhoa", true)[0] as DateEdit;

            data.FrmMain.Shown += new EventHandler(FrmMain_Shown);
            gvDiemDanh.RowCellStyle += new RowCellStyleEventHandler(gv_RowCellStyle);
            gvMTDK.CellValueChanged += new DevExpress.XtraGrid.Views.Base.CellValueChangedEventHandler(gvMTDK_CellValueChanged);
            gvMTDK.OptionsView.NewItemRowPosition = NewItemRowPosition.None; //không cho thêm mới
            gvMTDK.ActiveFilterString = "IsNghiHoc = 0 and IsBL = 0";
            gvMTDK.Columns["IsNghiHoc"].VisibleIndex = -1;
            gvMTDK.Columns["NgayNghi"].VisibleIndex = -1;
            gvMTDK.Columns["IsBL"].VisibleIndex = -1;
            gvMTDK.Columns["NgayBL"].VisibleIndex = -1;
            RepositoryItemCheckEdit riDuyet = gvMTDK.GridControl.RepositoryItems["Duyet"] as RepositoryItemCheckEdit;
            riDuyet.EditValueChanging += new DevExpress.XtraEditors.Controls.ChangingEventHandler(riDuyet_EditValueChanging);
            // Tao nut diem danh
            //SimpleButton btnDiemDanh = new SimpleButton();
            //btnDiemDanh.Name = "btnDiemDanh";
            //btnDiemDanh.Text = "Điểm danh";
            //LayoutControlItem lci = lcMain.AddItem("", btnDiemDanh);
            //lci.Name = "cusDiemDanh";
            //btnDiemDanh.Click += new EventHandler(btnDiemDanh_Click);

            //Tạo nút Ẩn hiện học viên nghỉ học
            SimpleButton btnAnHienHVNghiHoc = new SimpleButton();
            btnAnHienHVNghiHoc.Name = "btnAnHienHV";
            btnAnHienHVNghiHoc.Text = "Hiện học viên đã nghỉ";
            btnAnHienHVNghiHoc.Tag = "HIEN";
            LayoutControlItem lci2 = lcMain.AddItem("", btnAnHienHVNghiHoc);
            lci2.Name = "cusAnHienHV";
            btnAnHienHVNghiHoc.Click += new EventHandler(btnAnHienHVNghiHoc_Click);

            // Nút tính ngày kết thúc
            SimpleButton btnNgayKT = new SimpleButton();
            btnNgayKT.Name = "btnNgayKT";   //phai co name cua control
            btnNgayKT.Text = "Tính ngày KT";
            LayoutControlItem lci3 = lcMain.AddItem("", btnNgayKT);
            lci3.Name = "cusNgayKT"; //phai co name cua item, bat buoc phai co "cus" phai truoc
            btnNgayKT.Click += new EventHandler(btnNgayKT_Click);

            // Tao spinedit
            //SpinEdit sThang = new SpinEdit();
            //sThang.Name = "sThang";
            //sThang.Text = "Chọn tháng";
            //LayoutControlItem lci1 = lcMain.AddItem("", sThang);
            //lci1.Name = "cusThang";
            //sThang.ValueChanged += new EventHandler(sThang_ValueChanged);

            //data.BsMain.PositionChanged += new EventHandler(BsMain_PositionChanged);

            data.BsMain.DataSourceChanged += new EventHandler(BsMain_DataSourceChanged);
            BsMain_DataSourceChanged(data.BsMain, new EventArgs());

        }

        void riDuyet_EditValueChanging(object sender, DevExpress.XtraEditors.Controls.ChangingEventArgs e)
        {
            if (!Boolean.Parse(Config.GetValue("Admin").ToString()) && !Boolean.Parse(data.DrTable["sApprove"].ToString()))
            {
                XtraMessageBox.Show("Không có quyền thực hiện chức năng",
                    Config.GetValue("PackageName").ToString());
                e.Cancel = true;
            }
        }
        //dùng riêng cho trường Thiên Thần
        private void AnThongTin()
        {
            XtraTabControl tcMain = data.FrmMain.Controls.Find("tcMain", true)[0] as XtraTabControl;
            foreach (XtraTabPage p in tcMain.TabPages)
                if (p.Name != "tpMTDK" && p.Name != "tpTLNgayNghiLop")
                    p.PageVisible = false;
        }

        void BsMain_DataSourceChanged(object sender, EventArgs e)
        {
            if (data.BsMain.Current == null) return;
            drMaster = (data.BsMain.Current as DataRowView).Row;

            DataTable dtLop = (data.BsMain.DataSource as DataSet).Tables[0];
            dtLop.ColumnChanged += new DataColumnChangeEventHandler(dtLop_ColumnChanged);
        }

        void dtLop_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (e.Row.RowState == DataRowState.Deleted)
                return;
            if (e.Column.ColumnName.ToUpper() == "MANLOP" && e.Row["MaNLop"] != DBNull.Value)
                CreateMaLop(e.Row);
            List<string> lstLich = new List<string>();
            lstLich.AddRange(new string[] { "NgayBDKhoa", "MaGioHoc", "SoBuoi" });
            if (lstLich.Contains(e.Column.ColumnName))
            {
                e.Row["NgayKTKhoa"] = DBNull.Value;
                e.Row.EndEdit();
            }
        }
        void BsMain_PositionChanged(object sender, EventArgs e)
        {
            if (data.BsMain.Current == null) return;
            drMaster = (data.BsMain.Current as DataRowView).Row;
            if (drMaster.RowState == DataRowState.Added || drMaster.RowState == DataRowState.Deleted)
                return;
            if (!data.FrmMain.Visible)
                return;
            if (gvMTDK != null && gvMTDK.Editable)
                gvMTDK.OptionsView.NewItemRowPosition = NewItemRowPosition.None; //không cho thêm mới
            
            // Làm sao để fix TH modified ???
            visibleColumns(dtNgayHoc.Select(string.Format("MaGioHoc = '{0}'", drMaster["MaGioHoc"])), thang, nam);
        }

        #region HvKhoi ----- Hiện, Ẩn học viên đã nghỉ học
        void btnAnHienHVNghiHoc_Click(object sender, EventArgs e)
        {
            SimpleButton btn = sender as SimpleButton;
            if (btn == null) return;
            if (btn.Tag.ToString() == "HIEN")
            {
                gvMTDK.ActiveFilterString= "";
                btn.Tag = "AN";
                btn.Text = "Ẩn học viên đã nghỉ";
                gvMTDK.Columns["IsNghiHoc"].VisibleIndex = 0;
                gvMTDK.Columns["NgayNghi"].VisibleIndex = gvMTDK.Columns["IsNghiHoc"].VisibleIndex + 1;
                gvMTDK.Columns["IsBL"].VisibleIndex = gvMTDK.Columns["NgayNghi"].VisibleIndex + 1;
                gvMTDK.Columns["NgayBL"].VisibleIndex = gvMTDK.Columns["IsBL"].VisibleIndex + 1;
            }
            else
            {
                gvMTDK.ActiveFilterString = "IsNghiHoc = 0 and IsBL = 0";
                btn.Tag = "HIEN";
                btn.Text = "Hiện học viên đã nghỉ";
                gvMTDK.Columns["IsNghiHoc"].VisibleIndex = -1;
                gvMTDK.Columns["NgayNghi"].VisibleIndex = -1;
                gvMTDK.Columns["IsBL"].VisibleIndex = -1;
                gvMTDK.Columns["NgayBL"].VisibleIndex = -1;
                
            }
            gvMTDK.RefreshData();
            if (gvMTDK.RowCount == 0)
            {
                XtraMessageBox.Show("Lớp hiện chưa có học viên đăng ký học.",
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        #endregion

        void gvMTDK_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (drMaster.RowState == DataRowState.Deleted || dateNgayBD.Properties.ReadOnly)
                return;
            if (e.Column.ColumnEditName == "Duyet")
            {
                gvMTDK.SetFocusedRowCellValue(gvMTDK.Columns["NguoiDuyet"], (bool)e.Value ? Config.GetValue("UserName").ToString() : string.Empty);
            }
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

        DataTable dtNgayHoc;

        void FrmMain_Shown(object sender, EventArgs e)
        {
            if (drMaster == null || gvDiemDanh == null)
                return;
            gvDiemDanh.OptionsView.NewItemRowPosition = NewItemRowPosition.None;
            // Ẩn cột
            thang = Config.GetValue("KyKeToan") != null ? int.Parse(Config.GetValue("KyKeToan").ToString()) : DateTime.Today.Month;
            nam = Config.GetValue("NamLamViec") != null ? int.Parse(Config.GetValue("NamLamViec").ToString()) : DateTime.Today.Year;
            drMaster = (data.BsMain.Current as DataRowView).Row;
            dtNgayHoc = db.GetDataTable(@"SELECT MaGioHoc, Thu, Value FROM CTGioHoc");

            visibleColumns(dtNgayHoc.Select(string.Format("MaGioHoc = '{0}'",drMaster["MaGioHoc"])), thang, nam);
            
        }

        void btnDiemDanh_Click(object sender, EventArgs e)
        {
            if (drMaster.RowState == DataRowState.Deleted || tcMain == null)
                return;

            drMaster = (data.BsMain.Current as DataRowView).Row;
            foreach (XtraTabPage tabPage in tcMain.TabPages)
            {
                if (tabPage.Text.Contains("Điểm danh học viên"))
                {
                    tcMain.SelectedTabPage = tabPage;
                    break;
                }
            }

            if (dateNgayBD.Properties.ReadOnly)
            {
                // TH View

                return;
            }
            else
            {
                // TH Edit ...
                createDiemDanh(thang, nam);
            }
        }

        void sThang_ValueChanged(object sender, EventArgs e)
        {

        }

        #region Điểm danh
        // Tạo điểm danh ...
        // 1. Insert dữ liệu ở database
        // 2. Lấy danh sách học viên của lớp
        // 3. Insert học viên vào gridview
        // 4. Accecpt change gridview, update dữ liệu vào DiemDanhHV (ICD)
        private void createDiemDanh(int _thang, int _nam)
        {
            DataTable _dt = (data.BsMain.DataSource as DataSet).Tables[4];
            
            // TH chua co du lieu trong DiemDanhHV
            string s_ins = string.Format(@" EXEC sp_Insert_DiemDanhHV '{0}','{1}','{2}'; 
                                            SELECT	hv.MaLop, hv.HVID, hv.NgayDK, hv.IsNghiHoc, hv.NgayNghi
                                            FROM	MTDK hv
                                            WHERE	hv.MaLop = '{2}' AND hv.IsNghiHoc = 0
                                                    AND hv.NgayDK <= DATEADD(MM,1,CONVERT(DATETIME,'1/{0}/{1}',103))-1", _thang, _nam, drMaster["MaLop"]);
            if(_dt.Select(string.Format("MaLop = '{0}'",drMaster["MaLop"])).Length >0)
                s_ins += string.Format(@"AND hv.HVID NOT IN (SELECT DISTINCT MaHV FROM DiemDanhHV 
                                                            WHERE YEAR(Ngay)= '{1}' AND MONTH(Ngay) = '{0}') ", _thang, _nam);
            else
                s_ins += string.Format(@"AND hv.HVID IN (SELECT DISTINCT MaHV FROM DiemDanhHV 
                                                        WHERE YEAR(Ngay)= '{1}' AND MONTH(Ngay) = '{0}') ", _thang, _nam);

            using (DataTable dt = db.GetDataTable(s_ins))
            {
                foreach (DataRow dr in dt.Rows)
                {
                    gvDiemDanh.AddNewRow();
                    gvDiemDanh.SetFocusedRowCellValue(gvDiemDanh.Columns["MaLop"], dr["MaLop"]);
                    gvDiemDanh.SetFocusedRowCellValue(gvDiemDanh.Columns["MaHV"], dr["HVID"]);
                    gvDiemDanh.SetFocusedRowCellValue(gvDiemDanh.Columns["Thang"], _thang);
                    gvDiemDanh.SetFocusedRowCellValue(gvDiemDanh.Columns["Nam"], _nam);
                    // Set gia tri vao colum ngay
                    DateTime ngadk = (DateTime)dr["NgayDK"];
                    //DateTime ngaynghi = (DateTime)dr["NgayNghi"];
                    for (int i = 1; i <= 31; i++)
                    {
                        string col = i < 10 ? "0" + i.ToString() : i.ToString();
                        DateTime _date = new DateTime(_nam, _thang, 1);
                        _date = _date.AddMonths(1).AddDays(-1);// Ngay cuoi thang
                        if (i <= _date.Day)
                            _date = new DateTime(_nam, _thang, i);

                        if (ngadk <= _date
                            && (!(bool)dr["isNghiHoc"] || ((bool)dr["isNghiHoc"] && (DateTime)dr["NgayNghi"] > _date)))
                        {
                            if (gvDiemDanh.Columns[col].Visible)
                                gvDiemDanh.SetFocusedRowCellValue(gvDiemDanh.Columns[col], false);
                        }
                    }
                    gvDiemDanh.UpdateCurrentRow();
                }
            }
            gvDiemDanh.OptionsView.NewItemRowPosition = NewItemRowPosition.None;
        }

        private void visibleAllCol()
        {
            for (int i = 1; i <= 31; i++)
            {
                string col = i < 10 ? "0" + i.ToString() : i.ToString();
                gvDiemDanh.Columns[col].Visible = true;
            }
        }
        // Ẩn cột trong grid điểm danh học viên
        private void visibleColumns(DataRow[] drNgayHocs, int _thang, int _nam)
        {
            // Bang ngay nghi cua lop
            DataTable dtNgayNghi = (data.BsMain.DataSource as DataSet).Tables[3];
            for (int i = 1; i <= 31; i++)
            {
                string col = i < 10 ? "0" + i.ToString() : i.ToString();
                DateTime date = new DateTime(_nam, _thang, 1);
                date = date.AddMonths(1).AddDays(-1);// Ngay cuoi thang
                if (i <= date.Day)
                    date = new DateTime(_nam, _thang, i);
                bool _visible = false;
                gvDiemDanh.Columns[col].Visible = true;
                // Set font color Saturday - Sunday
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    gvDiemDanh.Columns[col].AppearanceCell.ForeColor = Color.Red;
                    gvDiemDanh.Columns[col].AppearanceHeader.ForeColor = Color.Red;
                }

                if (drNgayHocs != null)
                {
                    foreach (DataRow dr in drNgayHocs)
                    {
                        if (dr["Value"].ToString() == dayofweek(date.DayOfWeek).ToString())
                        {
                            _visible = true;
                            break;
                        }
                    }
                }

                // ngay nghi ko hien ra
                foreach (DataRow dr in dtNgayNghi.Select(string.Format("MaLop = '{0}'", drMaster["MaLop"])))
                {
                    if ((DateTime)dr["NgayNghi"] <= date && date <= (DateTime)dr["DenNgay"])
                    {
                        _visible = false;
                        break;
                    }
                }

                gvDiemDanh.Columns[col].Visible = _visible;
            }

            gvDiemDanh.BestFitColumns();
        }

        int dayofweek(DayOfWeek val)
        {
            switch (val)
            {
                case DayOfWeek.Monday:
                    return 2;
                case DayOfWeek.Tuesday:
                    return 3;
                case DayOfWeek.Wednesday:
                    return 4;
                case DayOfWeek.Thursday:
                    return 5;
                case DayOfWeek.Friday:
                    return 6;
                case DayOfWeek.Saturday:
                    return 7;
                case DayOfWeek.Sunday:
                    return 1;
            }
            return 0;
        }
                
        void gv_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (gvDiemDanh.GetRowCellValue(e.RowHandle, e.Column) == DBNull.Value 
                || gvDiemDanh.GetRowCellValue(e.RowHandle, e.Column) == null)
            {
                e.Appearance.BackColor = Color.LightGray;
            }
        }
        #endregion
        
        private void CreateMaLop(DataRow drLop)
        {
            string MaLopNew = "";

            // Malop = MaCN + STT
            string sql = string.Format(@"
                            SELECT TOP 1 MaLop, ISNULL(replace(MaLop, '{0}', ''), 1) [STT] -- Lấy số thứ tự lớp nhất
                            FROM DMLopHoc
                            WHERE MaNLop = '{0}' AND ISNUMERIC(replace(MaLop, '{0}', '')) = 1
                            ORDER BY cast(replace(MaLop, '{0}', '') as int) desc ", drLop["MaNLop"]);
            using (DataTable dt = db.GetDataTable(sql))
            {
                if (dt == null || dt.Rows.Count == 0)
                    MaLopNew = drLop["MaNLop"].ToString() + "1";
                else
                {
                    Int32 _stt = Convert.ToInt32(dt.Rows[0]["STT"].ToString()) + 1;
                    MaLopNew = drLop["MaNLop"].ToString() + _stt.ToString();
                }
            }
            if (!string.IsNullOrEmpty(MaLopNew))
                drLop["MaLop"] = MaLopNew;
            else
                XtraMessageBox.Show("Không tạo được mã lớp.Vui lòng kiểm tra lại dữ liệu.", Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }



        #region Tính Ngày KT Khóa

        void btnNgayKT_Click(object sender, EventArgs e)
        {
            if (data.BsMain.Current == null)
                return;
            if (dateNgayBD.Properties.ReadOnly)
            {
                XtraMessageBox.Show("Vui lòng chuyển sang chế độ chỉnh sửa hoặc thêm mới!",
                    Config.GetValue("PackageName").ToString(), MessageBoxButtons.OK);
                return;
            }

            drMaster = (data.BsMain.Current as DataRowView).Row;
            if (drMaster["MaLop"].ToString() == "")
                CreateMaLop(drMaster);
            if (dtCTGioHoc == null)
                dtCTGioHoc = db.GetDataTable(@"SELECT MaGioHoc, Thu, [Value] FROM CTGioHoc");
            NapLichNghi();
            DataTable disctDayOff = null;
            if (!CheckNgayBD(dateNgayBD.DateTime, ref disctDayOff))
                return;
            if (drMaster["MaGioHoc"] != DBNull.Value && drMaster["SoBuoi"] != DBNull.Value
                && drMaster["NgayBDKhoa"] != DBNull.Value && (decimal)drMaster["SoBuoi"] != 0)
            {
                DataRow[] drCTGioHoc = dtCTGioHoc.Select(string.Format(" MaGioHoc = '{0}' ", drMaster["MaGioHoc"].ToString()));
                if (drCTGioHoc.Length < 1)
                {
                    XtraMessageBox.Show("Thời gian học chưa thiết lập", Config.GetValue("PackageName").ToString());
                    return;
                }
                decimal dSoBuoi = (decimal)drMaster["SoBuoi"];
                DateTime NgayKT = TinhNgayKT(disctDayOff, drMaster["MaGioHoc"].ToString(), (DateTime)drMaster["NgayBDKhoa"]
                                        , Convert.ToInt32(dSoBuoi), drCTGioHoc);
                drMaster["NgayKTKhoa"] = NgayKT;
                dateNgayKT.DateTime = NgayKT;
            }
        }

        private bool CheckNgayBD(DateTime NgayBD, ref DataTable disctDayOff)
        {
            DataRow[] drCTGioHoc = dtCTGioHoc.Select(string.Format(" MaGioHoc = '{0}' ", drMaster["MaGioHoc"].ToString()));
            bool isDungLich = false;
            foreach (DataRow drGH in drCTGioHoc)
                if (NgayBD.DayOfWeek == OfWeek(drGH["Value"].ToString()))
                {
                    isDungLich = true;
                    break;
                }
            if (!isDungLich)
            {
                XtraMessageBox.Show(string.Format("Ngày bắt đầu không khớp với lịch học\n{0} <> {1}", NgayBD.DayOfWeek, drMaster["MaGioHoc"]),
                    Config.GetValue("PackageName").ToString());
                drMaster["NgayBDKhoa"] = DBNull.Value;
                return false;
            }
            DataTable dtDayOff = new DataTable();
            dtDayOff.Columns.Add("DayOff", typeof(DateTime));

            dtNgayNghi = (data.BsMain.DataSource as DataSet).Tables[3];
            DataRow[] drs = dtNgayNghi.Select(string.Format("MaLop = '{0}'", drMaster["MaLop"].ToString()));
            if (drs.Length != 0)
            {
                foreach (DataRow _dr in drs)
                {
                    DateTime TuNgay = (DateTime)_dr["NgayNghi"];

                    while (TuNgay <= (DateTime)_dr["DenNgay"])
                    {
                        DataRow drNew = dtDayOff.NewRow();
                        drNew["DayOff"] = TuNgay;
                        dtDayOff.Rows.Add(drNew);
                        TuNgay = TuNgay.AddDays(1);
                    }
                }
            }
            disctDayOff = dtDayOff.DefaultView.ToTable(true, "DayOff");
            bool isNgayNghi = false;
            foreach (DataRow drOff in disctDayOff.Rows)
            {
                DateTime date = (DateTime)drOff["DayOff"];
                if (date == NgayBD)
                {
                    isNgayNghi = true;
                    break;
                }
            }
            if (isNgayNghi)
            {
                XtraMessageBox.Show("Ngày bắt đầu trùng với lịch nghỉ",
                    Config.GetValue("PackageName").ToString());
                drMaster["NgayBDKhoa"] = DBNull.Value;
                return false;
            }
            return true;
        }

        private DateTime TinhNgayKT(DataTable disctDayOff, string MaGioHoc, DateTime NgayBD, int SoBuoi, DataRow[] drCTGioHoc)
        {
            int iCount = 1;
            int iOff = 0;// Số buổi trùng với lịch nghỉ
            DateTime NgayKT = NgayBD;

            while (iCount < SoBuoi)
            {
                NgayKT = NgayKT.AddDays(1);
                foreach (DataRow dr in drCTGioHoc)
                {
                    if (NgayKT.DayOfWeek == OfWeek(dr["Value"].ToString()))
                    {
                        foreach (DataRow drOff in disctDayOff.Rows)
                        {
                            DateTime _date = (DateTime)drOff["DayOff"];
                            if (_date == NgayKT)
                            {
                                iOff++;
                                break;
                            }
                        }
                        iCount++;
                        break;
                    }
                }
            }
            // Cộng thêm số buổi trùng với lịch nghỉ
            while (iOff != 0)
            {
                NgayKT = NgayKT.AddDays(1);
                foreach (DataRow dr in drCTGioHoc)
                {
                    if (NgayKT.DayOfWeek == OfWeek(dr["Value"].ToString()))
                    {
                        //Ngày tiếp theo có buổi trùng với lịch nghỉ
                        foreach (DataRow drOff in disctDayOff.Rows)
                        {
                            DateTime _date = (DateTime)drOff["DayOff"];
                            if (_date == NgayKT)
                            {
                                iOff++;
                                break;
                            }
                        }
                        iOff--;
                        break;
                    }
                }
            }

            return NgayKT;
        }

        private DayOfWeek OfWeek(string Value)
        {
            DayOfWeek _DayOfWeek = DayOfWeek.Monday;
            switch (Value)
            {
                case "2":
                    _DayOfWeek = DayOfWeek.Monday;
                    break;
                case "3":
                    _DayOfWeek = DayOfWeek.Tuesday;
                    break;
                case "4":
                    _DayOfWeek = DayOfWeek.Wednesday;
                    break;
                case "5":
                    _DayOfWeek = DayOfWeek.Thursday;
                    break;
                case "6":
                    _DayOfWeek = DayOfWeek.Friday;
                    break;
                case "7":
                    _DayOfWeek = DayOfWeek.Saturday;
                    break;
                case "1":
                    _DayOfWeek = DayOfWeek.Sunday;
                    break;
            }
            return _DayOfWeek;
        }

        private void NapLichNghi()
        {
            if (data.BsMain.Current == null)
                return;
            drMaster = (data.BsMain.Current as DataRowView).Row;
            if (dateNgayBD.Properties.ReadOnly)
            {
                XtraMessageBox.Show("Vui lòng chuyển sang chế độ chỉnh sửa hoặc thêm mới!",
                    Config.GetValue("PackageName").ToString(), MessageBoxButtons.OK);
                return;
            }
            DateTime dNgayBD;
            if (gvNgayNghi == null || dateNgayBD == null)
                return;

            if (string.IsNullOrEmpty(dateNgayBD.Text))
            {
                XtraMessageBox.Show("Ngày bắt đầu lớp không được rỗng", Config.GetValue("PackageName").ToString());
                return;
            }
            else
            {
                dNgayBD = dateNgayBD.DateTime;
            }

            string sql = string.Format(@"SELECT	NgayNghi, DenNgay , DienGiai
                                        FROM	TLNgayNghi
                                        WHERE	(YEAR('{0}') BETWEEN YEAR(NgayNghi) AND YEAR(DenNgay) OR YEAR('{0}') <= YEAR(DenNgay))
                                                AND DenNgay >= '{0}'
                                        ORDER BY NgayNghi", dNgayBD);
            DataTable _dtNgayNghi = db.GetDataTable(sql);
            DataTable dtNNLop = (data.BsMain.DataSource as DataSet).Tables["TLNgayNghiLop"];
            foreach (DataRow row in _dtNgayNghi.Rows)
            {
                bool check = true;
                if (gvNgayNghi.DataRowCount > 0)    //nếu đã có phải kiểm tra lại
                {
                    DataRow[] drs = dtNNLop.Select(string.Format("MaLop = '{0}' and NgayNghi = '{1}' and DenNgay = '{2}'",
                        drMaster["MaLop"], row["NgayNghi"], row["DenNgay"]));
                    if (drs.Length > 0)
                        check = false;
                    else
                        check = true;
                }
                if (check)
                {
                    DataRow drNN = dtNNLop.NewRow();
                    drNN["MaLop"] = drMaster["MaLop"];
                    drNN["NgayNghi"] = row["NgayNghi"];
                    drNN["DenNgay"] = row["DenNgay"];
                    drNN["DienGiai"] = row["DienGiai"];
                    dtNNLop.Rows.Add(drNN);
                }
            }

        }

        #endregion
    }
}
