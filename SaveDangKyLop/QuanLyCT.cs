using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using CDTDatabase;
using CDTLib;

namespace SaveDangKyLop
{
    public class QuanLyCT
    {
        private string _mahv;
        private string _tenhv;
        Database _db;

        public QuanLyCT(string mahv, string tenhv, Database db)
        {
            _mahv = mahv;
            _tenhv = tenhv;
            _db = db;
        }

        private string LaySoCT(string TableName, DateTime ngayct, string MaCN)
        {
            string Nam = ngayct.Year.ToString();
            string Thang = ngayct.Month.ToString();
            Nam = Nam.Substring(2, 2);
            string MaCT = "";
            if (TableName == "MT15") // phiếu báo có
                MaCT = "PBC";
            if (TableName == "MT11") // phiếu thu
                MaCT = "PT";
            if (TableName == "MT43") // phiếu xuất kho
                MaCT = "PXK";
            if (TableName == "MT31") // phiếu dịch vụ
                MaCT = "HDV";
            string prefix = MaCT + "/" + MaCN + "/" + Nam + "/" + Thang + "/";
            //string suffix = "/" + Nam + MaCN;

            string sql = string.Format(@"SELECT TOP 1 CAST(REPLACE(SoCT,'{0}','') AS INT) [SoCT]
                                        FROM	{1}
                                        WHERE	SoCT LIKE '{0}%' 
                                                AND ISNUMERIC(REPLACE(SoCT,'{0}','')) = 1
                                        ORDER BY CAST(REPLACE(SoCT,'{0}','') AS INT) DESC"
                                        , prefix, TableName);

            string soctNew;
            using (DataTable dt = _db.GetDataTable(sql))
            {
                if (dt.Rows.Count > 0)
                {
                    int i = (int)dt.Rows[0]["SoCT"] + 1;
                    soctNew = prefix + i.ToString("D3");
                }
                else
                {
                    soctNew = prefix + "001";
                }
            }
            return soctNew;
        }
        private string ReplaceDauNhayDon(string input)
        {
            return input.Replace("'", "''");
        }
        private string GetString_InsertMT43_BLVT(DataRow drThuHP)
        {
            string DTDKLopID = drThuHP["ID"].ToString();
            DateTime NGAYCT = (DateTime)drThuHP["NgayTN"];
            string MaCN = Config.GetValue("MaCN").ToString();
            string MT43ID = Guid.NewGuid().ToString();
            string MACT = "PXK";
            string SOCT = LaySoCT("MT43", NGAYCT, MaCN);
            string MAKH = _mahv;
            string TENKH = _tenhv;
            string TKCO = "632";
            string HTTT = drThuHP["HTTT"].ToString();
            string MACD = drThuHP["MaCD"].ToString();

            // INSERT MT43 + DT43
            string sql = string.Format(@"
                INSERT INTO MT43(MT43ID,MACT,SOCT,NGAYCT,MAKH,TENKH,DIENGIAI)
                VALUES('{0}','{1}','{2}','{3}','{4}',N'{5}',N'Xuất bán giáo trình')

                INSERT  INTO DT43(DT43ID,MT43ID,MAKHO,MABP,MAVT,TENVT,MADVT,SOLUONG,GIA,PS,TKCO,TKNO, REFDTDKLOP)
                SELECT  NEWID(),'{0}','{6}','{6}',VL.MAVT,VT.TENVT,VT.MADVT,1,VT.GIABAN,VT.GIABAN
                        ,'{9}',CASE WHEN '{7}' = 'TM' THEN '1111' ELSE '1121' END,'{10}'    
                FROM    VTNL VL 
                        INNER JOIN DMVT VT ON VT.MAVT = VL.MAVT                        
                WHERE   VL.MANLOP = '{8}'"
                    , MT43ID, MACT, SOCT, NGAYCT, MAKH, TENKH, MaCN, HTTT, MACD, TKCO, DTDKLopID);

            // INSERT BLVT
            sql += string.Format(@"
                        INSERT INTO BLVT(MTID, NgayCT, MaKH, TenKH, NhomDK, SoLuong, refDTDKLop)
                        SELECT	m.MT43ID, m.NgayCT, m.MaKH, m.TenKH, 'PXK1', 1, '{1}'
                        FROM    DT43 AS d 
                                INNER JOIN MT43 AS m ON d.MT43ID = m.MT43ID
                        WHERE	m.MT43ID = '{0}';", MT43ID, DTDKLopID);
            drThuHP["MT43ID"] = MT43ID;
            return sql;
        }
        public string GetStringInsertMT15_BLTK(DataRow drThuHP, bool isInsertMT43)
        {
            string sql = "";
            string DTDKLopID = drThuHP["ID"].ToString();
            string MT15ID = "";
            string DT15ID = Guid.NewGuid().ToString();
            DateTime NgayCT = (DateTime)drThuHP["NgayTN"];
            string MaCN = Config.GetValue("MaCN").ToString();
            string SoCT = LaySoCT("MT15", NgayCT, MaCN);
            string MaKH = _mahv;
            string TenKH = _tenhv;
            string DienGiai = "Thu học phí";
            string BPThu = MaCN;
            string TKNo = "1121";
            string TKCo = "5113";
            string DienGiaiCT = "";
            string NVTV = Config.GetValue("UserName").ToString();
            decimal TDaNop = (decimal)drThuHP["TDaNop"];
            decimal TienHP = TDaNop - (decimal)drThuHP["TienGT"];
            DienGiaiCT = "Thu học phí";   
         
            MT15ID = Guid.NewGuid().ToString();

            // INSERT MT15 + DT15
            sql = string.Format(@"                    
                    INSERT INTO MT15(MT15ID,SOCT,MAKH,TENKH,DIENGIAI,TKNO,NGAYCT,TTIEN)
                        VALUES('{0}','{1}','{2}',N'{3}',N'{4}','{5}','{6}',{7});
                    INSERT INTO DT15(DT15ID,MT15ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT, REFDTDKLOP,MABP)
                        VALUES('{8}','{0}',{9},'{10}','{2}',N'{3}',N'{11}','{12}','{13}');"
                    , MT15ID, SoCT, MaKH, TenKH, DienGiai, TKNo, NgayCT, TDaNop, DT15ID, TienHP, TKCo, DienGiaiCT, DTDKLopID, MaCN);
            // có Mua giáo trình
            if ((bool)drThuHP["isMuaBT"])
            {
                decimal TienBT = (decimal)drThuHP["TienGT"]; // tiền bàn tính
                TKCo = "5111";
                DienGiaiCT = "Mua giáo trình";
                sql += string.Format(@"
                                INSERT INTO DT15(DT15ID,MT15ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT, REFDTDKLOP, MABP)
                                VALUES(NEWID(),'{1}',{2},'{3}','{4}',N'{5}',N'{6}','{7}','{8}');"
                , DT15ID, MT15ID, TienBT, TKCo, MaKH, TenKH, DienGiaiCT, DTDKLopID, MaCN);

                if (isInsertMT43)
                    sql += GetString_InsertMT43_BLVT(drThuHP);
            }
            
            // Insert BLTK
            sql += string.Format(@"
                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT	m.MaCT, m.MT15ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, m.TkNo, d.TkCo, d.Ps, 0, 'PBC1', d.DT15ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT15 AS d 
                                INNER JOIN MT15 AS m ON d.MT15ID = m.MT15ID
                        WHERE	m.MT15ID = '{0}'

                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT  m.MaCT, m.MT15ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, d.TkCo, m.TkNo, 0, d.Ps, 'PBC2', d.DT15ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT15 AS d 
                                INNER JOIN MT15 AS m ON d.MT15ID = m.MT15ID
                        WHERE	m.MT15ID = '{0}'"
                        , MT15ID, DTDKLopID);
            // Thêm MT15ID vào field MT15ID(phiếu báo có) của table DTDKLop
            drThuHP["MT15ID"] = MT15ID;
            return sql;
        }
        public string GetStringInsertMT11_BLTK(DataRow drv, bool isInsertMT43)
        {
            string sql = "";
            string DTDKLopID = drv["ID"].ToString();
            string MT11ID = "";
            string DT11ID = Guid.NewGuid().ToString();
            string MaCN = Config.GetValue("MaCN").ToString();
            DateTime NgayCT = (DateTime)drv["NgayTN"];
            string SoCT = LaySoCT("MT11", NgayCT, MaCN);
            string MaKH = _mahv;
            string TenKH = _tenhv;
            string DienGiai = "Thu học phí";
            string BPThu = MaCN;
            string TKNo = "1111";
            string TKCo = "5113";
            string DienGiaiCT = "";
            string NVTV = Config.GetValue("UserName").ToString();
            decimal TDaNop = (decimal)drv["TDaNop"];
            decimal TienHP = TDaNop - (decimal)drv["TienGT"];

            DienGiaiCT = "Thu học phí";

            // NẾU THU HỌC PHÍ VỚI NGAYTHU ĐÃ ĐƯỢC LẬP TRONG MT11 THÌ LẤY MT11ID VỚI NGAYCT ĐÓ
            // NGƯỢC LẠI THÌ TẠO MỚI MT11ID.
            // *******  Nếu cần gộp phiếu thu thì uncomment những dòng này ******* //
            //string sql1 = string.Format("SELECT MT11ID FROM MT11 WHERE NGAYCT = '{0}' AND DUYET = 0 AND MAKH = '{1}'", NgayCT, MaKH);
            //object obj = _db.GetValue(sql1);
            //MT11ID = obj != null ? obj.ToString() : Guid.NewGuid().ToString();

            // *******  Nếu cần gộp phiếu thu thì hãy comment dòng này ******* //
            MT11ID = Guid.NewGuid().ToString();

            // INSERT MT11 + DT11
            sql = string.Format(@"              
                -- KIỂM TRA ĐÃ LẬP PHIẾU THU NÀO CÓ NGAYCT NHƯ VẬY CHƯA, 
                -- NẾU CÓ RỒI THÌ KHÔNG CẦN TẠO MỚI MT11 NỮA MÀ CHỈ CẦN ADD THÊM VÀO DT11
                -- Nếu cần gộp phiếu thu thì uncomment những dòng này
                /*IF NOT EXISTS (SELECT MT11ID FROM MT11 WHERE MT11ID = '{0}')   
                BEGIN                    
                    INSERT INTO MT11(MT11ID,SOCT,MAKH,TENKH,DIENGIAI,BPTHU,TKNO,NVTV,NGAYCT,TTIEN)
                    VALUES('{0}','{1}','{2}',N'{3}',N'{4}','{5}','{6}','{7}','{8}',{9});
                END*/ 
                
                INSERT INTO MT11(MT11ID,SOCT,MAKH,TENKH,DIENGIAI,BPTHU,TKNO,NVTV,NGAYCT,TTIEN)
                    VALUES('{0}','{1}','{2}',N'{3}',N'{4}','{5}','{6}','{7}','{8}',{9}); -- Nếu cần gộp phiếu thu thì comment những dòng insert này.
   
                INSERT INTO DT11(DT11ID,MT11ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT,REFDTDKLOP, MABP)
                    VALUES('{10}','{0}',{11},'{12}','{2}',N'{3}',N'{13}','{14}','{15}')"
                , MT11ID, SoCT, MaKH, TenKH, DienGiai, BPThu, TKNo, NVTV, NgayCT, TDaNop, DT11ID, TienHP, TKCo, DienGiaiCT, DTDKLopID, MaCN);
            // có Mua giáo trình
            if ((bool)drv["isMuaBT"])
            {
                decimal TienBT = (decimal)drv["TienGT"]; // tiền bàn tính
                TKCo = "5111";
                DienGiaiCT = "Mua giáo trình";
                sql += string.Format(@"
                                INSERT INTO DT11(DT11ID,MT11ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT,REFDTDKLOP, MABP)
                                VALUES(NEWID(),'{1}',{2},'{3}','{4}',N'{5}',N'{6}','{7}','{8}');"
                , DT11ID, MT11ID, TienBT, TKCo, MaKH, TenKH, DienGiaiCT, DTDKLopID, MaCN);

                if (isInsertMT43)
                    sql += GetString_InsertMT43_BLVT(drv);
            }
            // *******  Nếu cần gộp phiếu thu thì uncomment những dòng này ******* //
            //sql += string.Format(@"UPDATE MT11 SET TTIEN = (SELECT ISNULL(SUM(PS),0) FROM DT11 WHERE MT11ID = '{0}') WHERE MT11ID = '{0}'", MT11ID);
            
            // Insert BLTK
            sql += string.Format(@"
                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT	m.MaCT, m.MT11ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, m.TkNo, d.TkCo, d.Ps, 0, 'PT1', d.DT11ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT11 AS d 
                                INNER JOIN MT11 AS m ON d.MT11ID = m.MT11ID
                        WHERE	m.MT11ID = '{0}' -- AND NOT EXISTS (SELECT TOP 1 BLTKID FROM BLTK WHERE MTIDDT = D.DT11ID AND NHOMDK = 'PT1' -- Nếu cần gộp phiếu thu thì uncomment điều kiện này)

                        -- Nếu cần gộp phiếu thu thì uncomment những dòng này--
                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT  m.MaCT, m.MT11ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, d.TkCo, m.TkNo, 0, d.Ps, 'PT2', d.DT11ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT11 AS d 
                                INNER JOIN MT11 AS m ON d.MT11ID = m.MT11ID
                        WHERE	m.MT11ID = '{0}' -- AND NOT EXISTS (SELECT TOP 1 BLTKID FROM BLTK WHERE MTIDDT = D.DT11ID AND NHOMDK = 'PT2')-- Nếu cần gộp phiếu thu thì uncomment điều kiện này)"
                        , MT11ID, DTDKLopID);
            // Thêm MT11ID vào field MT11ID(phiếu thu) của table DTDKLop
            drv["MT11ID"] = MT11ID;
            return sql;
        }
        public string GetStringInsertMT31_BLTK(DataRow drv, bool isInsertMT43)
        {
            string sql = "";
            string DTDKLopID = drv["ID"].ToString();
            string MT31ID = "";
            string DT31ID = Guid.NewGuid().ToString();
            string MaCN = Config.GetValue("MaCN").ToString();
            DateTime NgayCT = (DateTime)drv["NgayTN"];
            string SoCT = LaySoCT("MT31", NgayCT, MaCN);
            string MaKH = _mahv;
            string TenKH = _tenhv;
            string DienGiai = "Thu học phí";
            string BPThu = MaCN;
            string TKNo = "131";
            string TKCo = "5113";
            string DienGiaiCT = "";
            string NVTV = Config.GetValue("UserName").ToString();
            decimal TDaNop = (decimal)drv["TDaNop"];
            decimal TienHP = TDaNop - (decimal)drv["TienGT"];
            DienGiaiCT = "Thu học phí";            

            MT31ID = Guid.NewGuid().ToString();

            // INSERT MT31 + DT31
            sql = string.Format(@"
                    DECLARE @TENKH NVARCHAR(128)
                    SELECT @TENKH = TRUONGLK From DMTRUONGLK WHERE ID = '{2}'                              
                    INSERT INTO MT31(MT31ID,SOCT,MAKH,TENKH,DIENGIAI,TKNO,NGAYCT,TTIEN)
                        VALUES('{0}','{1}','{2}',@TENKH,N'{4}','{6}','{8}',{9});{5}
                    INSERT INTO DT31(DT31ID,MT31ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT, REFDTDKLOP, MABP)
                        VALUES('{10}','{0}',{11},'{12}','{2}',N'{7}',N'{13}','{14}','{15}');{3}"
                    , MT31ID, SoCT, MaKH, "", DienGiai, "", TKNo, TenKH, NgayCT, TDaNop, DT31ID, TienHP, TKCo, DienGiaiCT, DTDKLopID, MaCN);
            // có Mua giáo trình
            if ((bool)drv["isMuaBT"])
            {
                decimal TienBT = (decimal)drv["TienGT"]; // tiền bàn tính
                TKCo = "5111";
                DienGiaiCT = "Mua giáo trình học viên " + TenKH;
                sql += string.Format(@"                        
                        INSERT INTO DT31(DT31ID,MT31ID,PS,TKCO,MAKHCT,TENKHCT,DIENGIAICT, REFDTDKLOP, MABP)
                        VALUES(NEWID(),'{1}',{2},'{3}','{4}',N'{5}',N'{6}','{7}','{8}');"
                , DT31ID, MT31ID, TienBT, TKCo, MaKH, TenKH, DienGiaiCT, DTDKLopID, MaCN);

                if (isInsertMT43)
                    sql += GetString_InsertMT43_BLVT(drv);
            }
            
            // Insert BLTK
            sql += string.Format(@"
                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT	m.MaCT, m.MT31ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, m.TkNo, d.TkCo, d.Ps, 0, 'HDV1', d.DT31ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT31 AS d 
                                INNER JOIN MT31 AS m ON d.MT31ID = m.MT31ID
                        WHERE	m.MT31ID = '{0}'

                        INSERT INTO BLTK(MaCT, MTID, SoCT, NgayCT, MaKH, TenKH, TK, TKDu, PsNo, PsCo, NhomDK, MTIDDT, MaBP, MaVV, refDTDKLop, DienGiai)
                        SELECT  m.MaCT, m.MT31ID, m.SoCT, m.NgayCT, m.MaKH, m.TenKH, d.TkCo, m.TkNo, 0, d.Ps, 'HDV2', d.DT31ID, d.MaBP, d.MaVV, '{1}', d.DienGiaiCT
                        FROM    DT31 AS d 
                                INNER JOIN MT31 AS m ON d.MT31ID = m.MT31ID
                        WHERE	m.MT31ID = '{0}'"
                        , MT31ID, DTDKLopID);
            // Thêm SoCT31 vào field MT31ID(phiếu thu) của table DTDKLop
            drv["MT31ID"] = MT31ID;
            return sql;
        }
        public void Delete_MT11_MT15_MT43_BLTK_BLVT(DataRow dr)
        {
            // Biến MT43ID dùng để nhận biết hàm này được gọi từ chức năng đăng ký theo lớp + quản lý học viên hay là được xóa từ chi tiết thu tiền nhiều lần.
            // nếu MT43ID == DBNull nghĩa là được gọi từ chi tiết thu tiền.
            object MT43ID = dr.Table.Columns.Contains("MT43ID") == true ? dr["MT43ID", DataRowVersion.Original] : DBNull.Value;
            object MT31ID = dr.Table.Columns.Contains("MT31ID") == true ? dr["MT31ID", DataRowVersion.Original] : DBNull.Value;

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
                    , MT31ID
                    , dr["MT11ID", DataRowVersion.Original]
                    , dr["MT15ID", DataRowVersion.Original]
                    , MT43ID);
            if (dr.Table.Columns.Contains("MT43ID") && dr.RowState != DataRowState.Deleted)
            {
                dr["MT11ID"] = DBNull.Value;
                dr["MT15ID"] = DBNull.Value;
                dr["MT31ID"] = DBNull.Value;
                dr["MT43ID"] = DBNull.Value;
            }
            // thực thi
            if(sql != "")
                _db.UpdateByNonQuery(sql);
        }
    }
}
