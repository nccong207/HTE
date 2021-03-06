using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using CDTDatabase;
using CDTLib;

namespace ChonLich
{
    public partial class frmChonLich : DevExpress.XtraEditors.XtraForm
    {
        private Database db = Database.NewDataDatabase();
        public string giohoc = "";
        //private int hvid = -1;
        private string hvid = "";
        public DataTable dtLuu = null;
        public frmChonLich(string hvid_ts)
        {
            hvid = hvid_ts;
            InitializeComponent();

        }

        private void frmChonLich_Load(object sender, EventArgs e)
        {
            string sql = "";
            sql = string.Format(@"   select	case when cl.id is null then cast(0 as bit) else cast(1 as bit) end [Chon]
	                                            ,l.id,n.diengiai
	                                            ,upper(datepart(hh,c.tgbd)) + ':' + upper(datepart(mi,c.tgbd)) + ' - ' + upper(datepart(hh,c.tgkt )) + ':' + upper(datepart(mi,c.tgkt)) [ThoiGian]
                                        from	dmlichhoc l
	                                            inner join dmngaygiohoc n on l.mangay = n.magiohoc
	                                            inner join dmca c on l.caid = c.maca
	                                            left join dtchonlich cl on l.id = cl.lichhocid and cl.hvtvid = {0}
                                        where	l.macn = '{1}'
                                        order by	n.thutu,datepart(hh,c.tgbd),datepart(mi,c.tgbd)
                                     ", (hvid==""?"-1":hvid),Config.GetValue("MaCN"));

            DataTable dtNgayGio = db.GetDataTable(sql);
            if (dtNgayGio.Rows.Count == 0)
            {
                XtraMessageBox.Show("Chưa có lịch học!");
                this.Close();
            }
            gridControl1.DataSource = dtNgayGio;  
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            DataView dvChon = new DataView(gridControl1.DataSource as DataTable);
            dvChon.RowFilter = "Chon = true";
            //Lưu table để dùng cho icd lưu dữ liệu
            dtLuu = dvChon.ToTable();
            if (dvChon.Count == 0)
            {
                XtraMessageBox.Show("Bạn chưa chọn lịch học!");
                return;
            }

            giohoc = "";
            foreach (DataRowView drv in dvChon)
            {
                giohoc += drv.Row["DienGiai"].ToString() + ": " + drv.Row["ThoiGian"].ToString() + "\r\n";
            }
            giohoc = giohoc.Substring(0,giohoc.Length - 2);
            this.DialogResult = DialogResult.OK;
            this.Close();
            //Xóa dữ liệu ô giờ học
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            //hủy
            this.Close();
        }

        private void frmChonLich_FormClosed(object sender, FormClosedEventArgs e)
        {
            //
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            DataView dvChon = new DataView(gridControl1.DataSource as DataTable);
            foreach (DataRowView drv in dvChon)
            {
                drv["Chon"] = (simpleButton3.Text == "Chọn tất cả");
            }
            simpleButton3.Text = simpleButton3.Text == "Chọn tất cả"?"Bỏ chọn tất cả":"Chọn tất cả";
        }
    }
}