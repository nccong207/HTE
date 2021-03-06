using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using CDTControl;
using DevExpress.XtraLayout.Utils;
using CDTLib;
using DevExpress.XtraLayout;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraCharts;
using Plugins;

namespace CDT
{
    public partial class FrmDashboard : DevExpress.XtraEditors.XtraForm
    {
        private SysMenu _sysMenu;
        private bool _isReminder;

        public bool IsReminder
        {
            get { return _isReminder; }
        }
        private int _soBaoCao = 0;

        private bool _isEmpty = false;
        private bool _first = true;

        public bool IsEmpty
        {
            get { return _isEmpty; }
        }

        public int SoBaoCao
        {
            get { return _soBaoCao; }
        }

        public FrmDashboard(SysMenu sysMenu, bool isReminder)
        {
            InitializeComponent();
            _sysMenu = sysMenu;
            _isReminder = isReminder;
            if (_isReminder)
            {
                this.Text = Config.GetValue("Language").ToString() == "0" ? "Hệ thống nhắc nhở" : "Notification";
                lciPeriod.Visibility = LayoutVisibility.Never;
                lciValue.Visibility = LayoutVisibility.Never;
                esiPeriod.Visibility = LayoutVisibility.Never;
                RefreshDb();
                object o = Config.GetValue("PhutNhacNho");
                decimal d = 0;
                if (o != null && Decimal.TryParse(o.ToString(), out d))
                {
                    timer1.Interval = (int)(60000 * d);
                    timer1.Start();
                }
            }
            else
                cboPeriod.SelectedIndex = 4;
            if (Config.GetValue("Language").ToString() != "0")
                FormFactory.DevLocalizer.Translate(this);
        }

        private void FrmDashboard_Load(object sender, EventArgs e)
        {
        }

        private void AddExtraReport(DataView dv, bool first)
        {
            int t = _isReminder && dv.Count == 3 ? 2 : 4;
            for (int i = t; i < dv.Count; i++)
            {
                //tao grid du lieu
                GridControl gc = new GridControl();
                gc.Name = "grid" + i.ToString();
                GridView gv = new GridView();
                gc.MainView = gv;
                gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gv });
                gv.GridControl = gc;
                gv.Appearance.Row.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
                gv.Appearance.Row.Options.UseForeColor = true;
                gv.OptionsBehavior.Editable = false;
                gv.OptionsSelection.EnableAppearanceFocusedCell = false;
                gv.OptionsSelection.EnableAppearanceFocusedRow = false;
                gv.OptionsView.EnableAppearanceEvenRow = false;
                //gv.OptionsView.ShowColumnHeaders = false;
                gv.OptionsView.ShowGroupPanel = false;
                gv.OptionsView.ShowHorzLines = true;
                gv.OptionsView.ShowIndicator = false;
                gv.OptionsView.ShowVertLines = false;
                gv.Appearance.HeaderPanel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold);
                gv.Appearance.HeaderPanel.Options.UseFont = true;
                gv.Appearance.HeaderPanel.Options.UseTextOptions = true;
                gv.Appearance.HeaderPanel.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
                Dashboard db = new Dashboard(dv[i].Row);
                //tao layoutcontrolitem chua grid
                int m = i + 3;
                LayoutItem libase = (i % 2 == 0) ? lcg2 : lcg6;
                LayoutControlItem lciNew = layoutControlGroup1.AddItem(libase, InsertType.Bottom);
                lciNew.AppearanceItemCaption.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold);
                lciNew.AppearanceItemCaption.ForeColor = System.Drawing.Color.SteelBlue;
                lciNew.AppearanceItemCaption.Options.UseFont = true;
                lciNew.AppearanceItemCaption.Options.UseForeColor = true;
                layoutControl1.Controls.Add(gc);
                lciNew.Control = gc;
                lciNew.Name = "lcg" + m.ToString();
                lciNew.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
                lciNew.Spacing = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
                lciNew.TextLocation = DevExpress.Utils.Locations.Top;
                lciNew.TextSize = new System.Drawing.Size(86, 15);
                db.GetGridReport(gc, first);
                string caption;
                if (Config.GetValue("Language").ToString() == "0")
                    caption = dv[i]["MenuName"].ToString() + " (" + gv.DataRowCount.ToString() + " mục)";
                else
                    caption = dv[i]["MenuName2"].ToString() + " (" + gv.DataRowCount.ToString() + " records)";
                lciNew.Text = caption;
                //layoutControlGroup1.AddItem(lciNew, layoutControlGroup1.Items[layoutControlGroup1.Items.Count - 1], InsertType.Bottom);
            }
        }

        private void RefreshDb()
        {
            string type = _isReminder ? "7" : "6";
            DataTable dtDb = _sysMenu.GetMenuForDashboard(type);
            if (_isReminder && dtDb.Rows.Count == 0)
            {
                this.Close();
                return;
            }
            _soBaoCao = dtDb.Rows.Count;

            DataView dv = new DataView(dtDb);
            dv.Sort = "MenuOrder";
            dv.RowFilter = "ChartField1 is null";
            if (dv.Count == 0)
            {
                lcg1.Visibility = LayoutVisibility.Never;
                lcg2.Visibility = LayoutVisibility.Never;
                lcg5.Visibility = LayoutVisibility.Never;
                lcg6.Visibility = LayoutVisibility.Never;
            }
            if (dv.Count >= 1)
            {
                lcg1.Visibility = LayoutVisibility.Always;
                Dashboard db = new Dashboard(dv[0].Row);
                db.GetGridReport(grid1, _first);
                string caption;
                if (Config.GetValue("Language").ToString() == "0")
                    caption = dv[0]["MenuName"].ToString() + " (" + grid1.Views[0].DataRowCount.ToString() + " mục)";
                else
                    caption = dv[0]["MenuName2"].ToString() + " (" + grid1.Views[0].DataRowCount.ToString() + " records)";
                lcg1.Text = caption;
            }
            if (dv.Count >= 2)
            {
                lcg2.Visibility = LayoutVisibility.Always;
                Dashboard db = new Dashboard(dv[1].Row);
                db.GetGridReport(grid2, _first);
                string caption;
                if (Config.GetValue("Language").ToString() == "0")
                    caption = dv[1]["MenuName"].ToString() + " (" + grid2.Views[0].DataRowCount.ToString() + " mục)";
                else
                    caption = dv[1]["MenuName2"].ToString() + " (" + grid2.Views[0].DataRowCount.ToString() + " records)";
                lcg2.Text = caption;
            }
            else
                lcg2.Visibility = LayoutVisibility.Never;
            if (dv.Count > 3)// && !_isReminder)
            {
                lcg5.Visibility = LayoutVisibility.Always;
                Dashboard db = new Dashboard(dv[2].Row);
                db.GetGridReport(grid3, _first);
                string caption;
                if (Config.GetValue("Language").ToString() == "0")
                    caption = dv[2]["MenuName"].ToString() + " (" + grid3.Views[0].DataRowCount.ToString() + " mục)";
                else
                    caption = dv[2]["MenuName2"].ToString() + " (" + grid3.Views[0].DataRowCount.ToString() + " records)";
                lcg5.Text = caption;
            }
            else
                lcg5.Visibility = LayoutVisibility.Never;
            if (dv.Count >= 4)
            {
                lcg6.Visibility = LayoutVisibility.Always;
                Dashboard db = new Dashboard(dv[3].Row);
                db.GetGridReport(grid4, _first);
                string caption;
                if (Config.GetValue("Language").ToString() == "0")
                    caption = dv[3]["MenuName"].ToString() + " (" + grid4.Views[0].DataRowCount.ToString() + " mục)";
                else
                    caption = dv[3]["MenuName2"].ToString() + " (" + grid4.Views[0].DataRowCount.ToString() + " records)";
                lcg6.Text = caption;
            }
            else
                lcg6.Visibility = LayoutVisibility.Never;
            if (dv.Count >= 5 || (dv.Count >= 3 && _isReminder))
                AddExtraReport(dv, _first);
            int c;
            if (_isReminder && dv.Count <= 3)
                c = 1;
            else
                c = 2;
            int n = dv.Count == 0 ? 0 : layoutControl1.Height / dv.Count * c;
            dv.RowFilter = "ChartField1 is not null";
            if (dv.Count == 0)
            {
                lcg3.Visibility = LayoutVisibility.Never;
                lcg4.Visibility = LayoutVisibility.Never;
                lcg7.Visibility = LayoutVisibility.Never;
                lcg8.Visibility = LayoutVisibility.Never;
                lcg1.Size = new Size(layoutControl1.Width / 2, lcg1.Size.Height);
                lcg2.Size = new Size(layoutControl1.Width / 2, lcg2.Size.Height);
            }
            dv.RowFilter = "ChartField1 is not null and ChartField3 is null";
            if (dv.Count >= 1)
            {
                lcg3.Visibility = LayoutVisibility.Always;
                lcg3.Size = new Size(layoutControl1.Width / dv.Count, layoutControl1.Height / 2);
                Dashboard db = new Dashboard(dv[0].Row);
                db.GetPieChartReport(chart1, _first);
                if (dv.Count >= 2)
                {
                    lcg7.Visibility = LayoutVisibility.Always;
                    lcg7.Size = new Size(layoutControl1.Width / dv.Count, layoutControl1.Height / 2);
                    db = new Dashboard(dv[1].Row);
                    db.GetPieChartReport(chart2, _first);
                    if (dv.Count >= 3)
                    {
                        lcg8.Visibility = LayoutVisibility.Always;
                        lcg8.Size = new Size(layoutControl1.Width / dv.Count, layoutControl1.Height / 2);
                        db = new Dashboard(dv[2].Row);
                        db.GetPieChartReport(chart3, _first);
                    }
                    else
                        lcg8.Visibility = LayoutVisibility.Never;
                }
                else
                {
                    lcg7.Visibility = LayoutVisibility.Never;
                    lcg8.Visibility = LayoutVisibility.Never;
                }
            }
            else
            {
                lcg3.Visibility = LayoutVisibility.Never;
                lcg7.Visibility = LayoutVisibility.Never;
                lcg8.Visibility = LayoutVisibility.Never;
            }
            dv.RowFilter = "ChartField1 is not null and ChartField3 is not null";
            if (dv.Count >= 1)
            {
                lcg4.Visibility = LayoutVisibility.Always;
                lcg4.Size = new Size(layoutControl1.Width, layoutControl1.Height / 2);
                Dashboard db = new Dashboard(dv[0].Row);
                db.GetChartReport(chart4, _first);
            }
            else
                lcg4.Visibility = LayoutVisibility.Never;
            if (gridView1.DataRowCount == 0 && gridView2.DataRowCount == 0 && gridView3.DataRowCount == 0 && gridView4.DataRowCount == 0)
                _isEmpty = true;
            //resize layout
            bool flag = dtDb.Select("ChartField1 is null").Length > 0 && !_isReminder;
            for (int i = 0; i < layoutControlGroup1.Items.Count; i++)
            {
                if (flag && layoutControlGroup1.Items[i] != lciPeriod &&
                    layoutControlGroup1.Items[i].Visibility == LayoutVisibility.Always)
                    layoutControlGroup1.Items[i].Size = new Size(layoutControl1.Width / dtDb.Rows.Count * 2, layoutControlGroup1.Items[i].Size.Height);
                if (layoutControlGroup1.Items[i] != lcg3 && layoutControlGroup1.Items[i] != lcg4 &&
                    layoutControlGroup1.Items[i] != lcg7 && layoutControlGroup1.Items[i] != lcg8 &&
                    layoutControlGroup1.Items[i] != lciPeriod &&
                    layoutControlGroup1.Items[i].Visibility == LayoutVisibility.Always)
                    layoutControlGroup1.Items[i].Size = new Size(layoutControlGroup1.Items[i].Size.Width, n);
            }
            _first = false;
        }

        private int GetFromQuarter(int month)
        {
            int tmp;
            if (month >= 1 && month <= 3)
                tmp = 1;
            else
            {
                if (month >= 4 && month <= 6)
                    tmp = 4;
                else
                {
                    if (month >= 7 && month <= 9)
                        tmp = 7;
                    else
                        tmp = 10;
                }
            }
            return tmp;
        }

        private int GetFromMonth(int quarter)
        {
            int tmp;
            switch (quarter)
            {
                case 1:
                    tmp = 1;
                    break;
                case 2:
                    tmp = 4;
                    break;
                case 3:
                    tmp = 7;
                    break;
                default:
                    tmp = 10;
                    break;
            }
            return tmp;
        }

        private void cboPeriod_SelectedIndexChanged(object sender, EventArgs e)
        {
            int month, year;
            if (Config.GetValue("NamLamViec") != null)
                year = Int32.Parse(Config.GetValue("NamLamViec").ToString());
            else
                year = DateTime.Today.Year;
            if (Config.GetValue("KyKeToan") != null)
                month = Int32.Parse(Config.GetValue("KyKeToan").ToString());
            else
                month = DateTime.Today.Month;
            DateTime fromDate;
            DateTime toDate;
            int m1 = 0, m2 = 0;
            switch (cboPeriod.SelectedIndex)
            {
                case 0:
                    m1 = m2 = month;
                    fromDate = DateTime.Parse(month.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(1).AddDays(-1);
                    break;
                case 1:
                    m1 = m2 = month == 1 ? 1 : month - 1;
                    fromDate = DateTime.Parse(m1.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(1).AddDays(-1);
                    break;
                case 2:
                    m1 = GetFromQuarter(month);
                    m2 = m1 + 2;
                    fromDate = DateTime.Parse(m1.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(3).AddDays(-1);
                    break;
                case 3:
                    m1 = GetFromQuarter(month);
                    m1 = m1 == 1 ? 1 : m1 - 3;
                    m2 = m1 + 2;
                    fromDate = DateTime.Parse(m1.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(3).AddDays(-1);
                    break;
                case 4:
                    m1 = 1;
                    m2 = 12;
                    fromDate = DateTime.Parse("01/01/" + year.ToString());
                    toDate = fromDate.AddMonths(12).AddDays(-1);
                    break;
                case 5:
                case 6:
                case 7:
                case 8:
                    m1 = GetFromMonth(cboPeriod.SelectedIndex - 4);
                    m2 = m1 + 2;
                    fromDate = DateTime.Parse(m1.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(3).AddDays(-1);
                    break;
                default:    //cac thang tu 1 den 12
                    m1 = m2 = cboPeriod.SelectedIndex - 8;
                    fromDate = DateTime.Parse(m1.ToString() + "/01/" + year.ToString());
                    toDate = fromDate.AddMonths(1).AddDays(-1);
                    break;
            }

            Config.NewKeyValue("@Thang1", m1);
            Config.NewKeyValue("@Thang2", m2);
            Config.NewKeyValue("@Thang", m1);
            Config.NewKeyValue("@NgayCT1", fromDate.Date);
            Config.NewKeyValue("@NgayCT2", toDate.Date);
            Config.NewKeyValue("@NgayCT", toDate.Date);
            RefreshDb();
        }

        private void FrmDashboard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
            if (e.KeyCode == Keys.F5)
                RefreshDb();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            RefreshDb();
        }

        private void ceValue_CheckedChanged(object sender, EventArgs e)
        {
            if (lcg3.Visibility == LayoutVisibility.Always)
                ((PiePointOptions)chart1.Series[0].PointOptions).PercentOptions.ValueAsPercent = ceValue.Checked;
            if (lcg7.Visibility == LayoutVisibility.Always)
                ((PiePointOptions)chart2.Series[0].PointOptions).PercentOptions.ValueAsPercent = ceValue.Checked;
            if (lcg8.Visibility == LayoutVisibility.Always)
                ((PiePointOptions)chart3.Series[0].PointOptions).PercentOptions.ValueAsPercent = ceValue.Checked;
            ((PiePointOptions)chart1.Series[0].PointOptions).ValueNumericOptions.Format = (ceValue.Checked) ? NumericFormat.Percent : NumericFormat.Number;
            ((PiePointOptions)chart2.Series[0].PointOptions).ValueNumericOptions.Format = (ceValue.Checked) ? NumericFormat.Percent : NumericFormat.Number;
            ((PiePointOptions)chart3.Series[0].PointOptions).ValueNumericOptions.Format = (ceValue.Checked) ? NumericFormat.Percent : NumericFormat.Number;
            ((PieSeriesLabel)chart1.Series[0].Label).Position = (ceValue.Checked) ? PieSeriesLabelPosition.TwoColumns : PieSeriesLabelPosition.Outside;
            ((PieSeriesLabel)chart2.Series[0].Label).Position = (ceValue.Checked) ? PieSeriesLabelPosition.TwoColumns : PieSeriesLabelPosition.Outside;
            ((PieSeriesLabel)chart3.Series[0].Label).Position = (ceValue.Checked) ? PieSeriesLabelPosition.TwoColumns : PieSeriesLabelPosition.Outside;
        }
    }
}