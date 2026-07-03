using Microsoft.Data.Sqlite;
using ISO11820.Models;

namespace ISO11820.Data;

/// <summary>
/// SQLite 数据库操作封装 — 直接写 SQL，无 ORM
/// </summary>
public class DbHelper
{
    private readonly string _connStr;

    public DbHelper(string dbPath)
    {
        _connStr = $"Data Source={dbPath}";
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        // 启用外键约束
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    #region 数据库初始化

    /// <summary>
    /// 创建所有表并写入初始数据（首次运行时）
    /// </summary>
    public void InitializeDatabase()
    {
        using var conn = OpenConnection();

        // ===== 建表 =====
        var createTables = new[]
        {
            // operators
            @"CREATE TABLE IF NOT EXISTS operators (
                userid   TEXT NOT NULL,
                username TEXT NOT NULL,
                pwd      TEXT NOT NULL,
                usertype TEXT NOT NULL
            );",

            // apparatus
            @"CREATE TABLE IF NOT EXISTS apparatus (
                apparatusid   INTEGER NOT NULL PRIMARY KEY,
                innernumber   TEXT NOT NULL,
                apparatusname TEXT NOT NULL,
                checkdatef    date NOT NULL,
                checkdatet    date NOT NULL,
                pidport       TEXT NOT NULL,
                powerport     TEXT NOT NULL,
                constpower    INTEGER NULL
            );",

            // productmaster
            @"CREATE TABLE IF NOT EXISTS productmaster (
                productid   TEXT NOT NULL PRIMARY KEY,
                productname TEXT NOT NULL,
                specific    TEXT NOT NULL,
                diameter    REAL NOT NULL,
                height      REAL NOT NULL,
                flag        TEXT NULL
            );",

            // testmaster
            @"CREATE TABLE IF NOT EXISTS testmaster (
                productid        TEXT NOT NULL,
                testid           TEXT NOT NULL,
                testdate         date NOT NULL,
                ambtemp          REAL NOT NULL,
                ambhumi          REAL NOT NULL,
                according        TEXT NOT NULL,
                operator         TEXT NOT NULL,
                apparatusid      TEXT NOT NULL,
                apparatusname    TEXT NOT NULL,
                apparatuschkdate date NOT NULL,
                rptno            TEXT NOT NULL,
                preweight        REAL NOT NULL,
                postweight       REAL NOT NULL,
                lostweight       REAL NOT NULL,
                lostweight_per   REAL NOT NULL,
                totaltesttime    INTEGER NOT NULL,
                constpower       INTEGER NOT NULL,
                phenocode        TEXT NOT NULL,
                flametime        INTEGER NOT NULL,
                flameduration    INTEGER NOT NULL,
                maxtf1           REAL NOT NULL,
                maxtf2           REAL NOT NULL,
                maxts            REAL NOT NULL,
                maxtc            REAL NOT NULL,
                maxtf1_time      INTEGER NOT NULL,
                maxtf2_time      INTEGER NOT NULL,
                maxts_time       INTEGER NOT NULL,
                maxtc_time       INTEGER NOT NULL,
                finaltf1         REAL NOT NULL,
                finaltf2         REAL NOT NULL,
                finalts          REAL NOT NULL,
                finaltc          REAL NOT NULL,
                finaltf1_time    INTEGER NOT NULL,
                finaltf2_time    INTEGER NOT NULL,
                finalts_time     INTEGER NOT NULL,
                finaltc_time     INTEGER NOT NULL,
                deltatf1         REAL NOT NULL,
                deltatf2         REAL NOT NULL,
                deltatf          REAL NOT NULL,
                deltats          REAL NOT NULL,
                deltatc          REAL NOT NULL,
                memo             TEXT NULL,
                flag             TEXT NULL,
                PRIMARY KEY (productid, testid),
                FOREIGN KEY (productid) REFERENCES productmaster(productid)
            );",

            // sensors
            @"CREATE TABLE IF NOT EXISTS sensors (
                sensorid    INTEGER NOT NULL PRIMARY KEY,
                sensorname  TEXT NOT NULL,
                dispname    TEXT NOT NULL,
                sensorgroup TEXT NOT NULL,
                unit        TEXT NOT NULL,
                discription TEXT NOT NULL,
                flag        TEXT NOT NULL,
                signalzero  REAL NOT NULL,
                signalspan  REAL NOT NULL,
                outputzero  REAL NOT NULL,
                outputspan  REAL NOT NULL,
                outputvalue REAL NOT NULL,
                inputvalue  REAL NOT NULL,
                signaltype  INTEGER NOT NULL
            );",

            // CalibrationRecords
            @"CREATE TABLE IF NOT EXISTS CalibrationRecords (
                Id                 TEXT NOT NULL PRIMARY KEY,
                CalibrationDate    TEXT NOT NULL,
                CalibrationType    TEXT NOT NULL,
                ApparatusId        INTEGER NOT NULL,
                Operator           TEXT NOT NULL,
                TemperatureData    TEXT NOT NULL,
                UniformityResult   REAL NULL,
                MaxDeviation       REAL NULL,
                AverageTemperature REAL NULL,
                PassedCriteria     INTEGER NOT NULL,
                Remarks            TEXT NOT NULL,
                CreatedAt          TEXT NOT NULL,
                TempA1 REAL NULL, TempA2 REAL NULL, TempA3 REAL NULL,
                TempB1 REAL NULL, TempB2 REAL NULL, TempB3 REAL NULL,
                TempC1 REAL NULL, TempC2 REAL NULL, TempC3 REAL NULL,
                TAvg        REAL NULL,
                TAvgAxis1   REAL NULL, TAvgAxis2 REAL NULL, TAvgAxis3 REAL NULL,
                TAvgLevela  REAL NULL, TAvgLevelb REAL NULL, TAvgLevelc REAL NULL,
                TDevAxis1   REAL NULL, TDevAxis2 REAL NULL, TDevAxis3 REAL NULL,
                TDevLevela  REAL NULL, TDevLevelb REAL NULL, TDevLevelc REAL NULL,
                TAvgDevAxis REAL NULL, TAvgDevLevel REAL NULL,
                CenterTempData TEXT NULL,
                Memo           TEXT NULL
            );"
        };

        foreach (var sql in createTables)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // ===== 创建索引 =====
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS IX_Testmaster_Testdate ON testmaster(testdate);",
            "CREATE INDEX IF NOT EXISTS IX_Testmaster_Operator ON testmaster(operator);",
            "CREATE INDEX IF NOT EXISTS IX_Testmaster_Testdate_Productid ON testmaster(testdate, productid);",
            "CREATE INDEX IF NOT EXISTS IX_CalibrationRecord_Date ON CalibrationRecords(CalibrationDate);",
            "CREATE INDEX IF NOT EXISTS IX_CalibrationRecord_Operator ON CalibrationRecords(Operator);"
        };

        foreach (var sql in indexes)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // ===== 初始数据 =====
        // 操作员
        InsertIfNotExists(conn,
            "INSERT INTO operators (userid, username, pwd, usertype) SELECT '1', 'admin', '123456', 'admin' WHERE NOT EXISTS (SELECT 1 FROM operators WHERE username = 'admin')");
        InsertIfNotExists(conn,
            "INSERT INTO operators (userid, username, pwd, usertype) SELECT '2', 'experimenter', '123456', 'operator' WHERE NOT EXISTS (SELECT 1 FROM operators WHERE username = 'experimenter')");

        // 设备
        InsertIfNotExists(conn,
            "INSERT INTO apparatus SELECT 0, 'FURNACE-01', '一号试验炉', date('now'), date('now','+1 year'), 'COM9', 'COM9', 2048 WHERE NOT EXISTS (SELECT 1 FROM apparatus WHERE apparatusid = 0)");

        // 传感器 — 通道 0~3（业务主要使用）和 16（校准）
        var sensorInitData = new[]
        {
            (0, "Sensor0", "炉温1", "采集", "炉温1"),
            (1, "Sensor1", "炉温2", "采集", "炉温2"),
            (2, "Sensor2", "表面温度", "采集", "表面温度"),
            (3, "Sensor3", "中心温度", "采集", "中心温度"),
        };

        for (int i = 0; i <= 3; i++)
        {
            var data = sensorInitData[i];
            InsertIfNotExists(conn,
                $"INSERT INTO sensors SELECT {data.Item1},'{data.Item2}','{data.Item3}','{data.Item4}','℃','{data.Item5}','启用',0,0,0,1000,0,0,4 WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = {data.Item1})");
        }

        // 校准通道 16
        InsertIfNotExists(conn,
            "INSERT INTO sensors SELECT 16,'Sensor16','校准温度','校准','℃','校准温度','启用',0,0,0,1000,0,0,4 WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 16)");

        // 备用通道 4~15
        for (int i = 4; i <= 15; i++)
        {
            InsertIfNotExists(conn,
                $"INSERT INTO sensors SELECT {i},'Sensor{i}','备用通道{i+1}','备用','℃','备用通道','启用',0,0,0,1000,0,0,4 WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = {i})");
        }
    }

    private void InsertIfNotExists(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 登录

    /// <summary>
    /// 登录验证 — 按 username + pwd 校验
    /// </summary>
    public bool Login(string username, string pwd, out string userId, out string userType)
    {
        userId = ""; userType = "";
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT userid, usertype FROM operators WHERE username=$name AND pwd=$pwd";
        cmd.Parameters.AddWithValue("$name", username);
        cmd.Parameters.AddWithValue("$pwd", pwd);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            userId = reader.GetString(0);
            userType = reader.GetString(1);
            return true;
        }
        return false;
    }

    #endregion

    #region 设备操作

    /// <summary>
    /// 获取当前设备信息
    /// </summary>
    public Apparatus? GetApparatus()
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM apparatus LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadApparatus(reader);
        return null;
    }

    /// <summary>
    /// 更新设备恒功率值
    /// </summary>
    public void UpdateConstPower(int apparatusId, int constPower)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE apparatus SET constpower=$cp WHERE apparatusid=$id";
        cmd.Parameters.AddWithValue("$cp", constPower);
        cmd.Parameters.AddWithValue("$id", apparatusId);
        cmd.ExecuteNonQuery();
    }

    private Apparatus ReadApparatus(SqliteDataReader reader)
    {
        return new Apparatus
        {
            ApparatusId = reader.GetInt32(reader.GetOrdinal("apparatusid")),
            InnerNumber = reader.GetString(reader.GetOrdinal("innernumber")),
            ApparatusName = reader.GetString(reader.GetOrdinal("apparatusname")),
            CheckDateF = DateTime.Parse(reader.GetString(reader.GetOrdinal("checkdatef"))),
            CheckDateT = DateTime.Parse(reader.GetString(reader.GetOrdinal("checkdatet"))),
            PidPort = reader.GetString(reader.GetOrdinal("pidport")),
            PowerPort = reader.GetString(reader.GetOrdinal("powerport")),
            ConstPower = reader.IsDBNull(reader.GetOrdinal("constpower"))
                ? null : reader.GetInt32(reader.GetOrdinal("constpower"))
        };
    }

    #endregion

    #region 样品操作

    /// <summary>
    /// 插入或替换样品信息
    /// </summary>
    public void UpsertProduct(ProductMaster product)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO productmaster
            (productid, productname, specific, diameter, height, flag)
            VALUES ($pid, $pname, $spec, $dia, $h, $flag)";
        cmd.Parameters.AddWithValue("$pid", product.ProductId);
        cmd.Parameters.AddWithValue("$pname", product.ProductName);
        cmd.Parameters.AddWithValue("$spec", product.Specific);
        cmd.Parameters.AddWithValue("$dia", product.Diameter);
        cmd.Parameters.AddWithValue("$h", product.Height);
        cmd.Parameters.AddWithValue("$flag", (object?)product.Flag ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 根据样品编号查询样品
    /// </summary>
    public ProductMaster? GetProduct(string productId)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM productmaster WHERE productid=$pid";
        cmd.Parameters.AddWithValue("$pid", productId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadProduct(reader);
        return null;
    }

    private ProductMaster ReadProduct(SqliteDataReader reader)
    {
        return new ProductMaster
        {
            ProductId = reader.GetString(reader.GetOrdinal("productid")),
            ProductName = reader.GetString(reader.GetOrdinal("productname")),
            Specific = reader.GetString(reader.GetOrdinal("specific")),
            Diameter = reader.GetDouble(reader.GetOrdinal("diameter")),
            Height = reader.GetDouble(reader.GetOrdinal("height")),
            Flag = reader.IsDBNull(reader.GetOrdinal("flag")) ? null : reader.GetString(reader.GetOrdinal("flag"))
        };
    }

    #endregion

    #region 试验操作

    /// <summary>
    /// 新建试验（初始插入，统计字段填0）
    /// </summary>
    public void InsertTest(TestMaster test)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO testmaster
                (productid, testid, testdate, operator, ambtemp, ambhumi,
                 according, apparatusid, apparatusname, apparatuschkdate, rptno,
                 preweight, postweight, lostweight, lostweight_per,
                 totaltesttime, constpower, phenocode, flametime, flameduration,
                 maxtf1, maxtf2, maxts, maxtc,
                 maxtf1_time, maxtf2_time, maxts_time, maxtc_time,
                 finaltf1, finaltf2, finalts, finaltc,
                 finaltf1_time, finaltf2_time, finalts_time, finaltc_time,
                 deltatf1, deltatf2, deltatf, deltats, deltatc, memo)
            VALUES
                ($pid, $tid, $tdate, $op, $at, $ah,
                 $acc, $aid, $aname, $achk, $rpt,
                 $prewt, 0, 0, 0,
                 0, 0, '', 0, 0,
                 0,0,0,0, 0,0,0,0,
                 0,0,0,0, 0,0,0,0,
                 0,0,0,0,0, $memo)";
        cmd.Parameters.AddWithValue("$pid", test.ProductId);
        cmd.Parameters.AddWithValue("$tid", test.TestId);
        cmd.Parameters.AddWithValue("$tdate", test.TestDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$op", test.Operator);
        cmd.Parameters.AddWithValue("$at", test.AmbTemp);
        cmd.Parameters.AddWithValue("$ah", test.AmbHumi);
        cmd.Parameters.AddWithValue("$acc", test.According);
        cmd.Parameters.AddWithValue("$aid", test.ApparatusId);
        cmd.Parameters.AddWithValue("$aname", test.ApparatusName);
        cmd.Parameters.AddWithValue("$achk", test.ApparatusChkDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$rpt", test.RptNo);
        cmd.Parameters.AddWithValue("$prewt", test.PreWeight);
        cmd.Parameters.AddWithValue("$memo", (object?)test.Memo ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 试验完成后更新统计字段
    /// </summary>
    public void UpdateTestResult(TestMaster test)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE testmaster SET
                postweight      = $post,
                lostweight      = $lost,
                lostweight_per  = $lostper,
                deltatf1        = $dtf1,
                deltatf2        = $dtf2,
                deltatf         = $dtf,
                deltats         = $dts,
                deltatc         = $dtc,
                finaltf1        = $ftf1,
                finaltf2        = $ftf2,
                finalts         = $fts,
                finaltc         = $ftc,
                finaltf1_time   = $ftf1t,
                finaltf2_time   = $ftf2t,
                finalts_time    = $ftst,
                finaltc_time    = $ftct,
                maxtf1          = $mtf1,
                maxtf2          = $mtf2,
                maxts           = $mts,
                maxtc           = $mtc,
                maxtf1_time     = $mtf1t,
                maxtf2_time     = $mtf2t,
                maxts_time      = $mtst,
                maxtc_time      = $mtct,
                totaltesttime   = $time,
                constpower      = $cp,
                phenocode       = $pheno,
                flametime       = $ftime,
                flameduration   = $fdur,
                memo            = $memo,
                flag            = '10000000'
            WHERE productid=$pid AND testid=$tid";
        cmd.Parameters.AddWithValue("$post", test.PostWeight);
        cmd.Parameters.AddWithValue("$lost", test.LostWeight);
        cmd.Parameters.AddWithValue("$lostper", test.LostWeightPer);
        cmd.Parameters.AddWithValue("$dtf1", test.DeltaTf1);
        cmd.Parameters.AddWithValue("$dtf2", test.DeltaTf2);
        cmd.Parameters.AddWithValue("$dtf", test.DeltaTf);
        cmd.Parameters.AddWithValue("$dts", test.DeltaTs);
        cmd.Parameters.AddWithValue("$dtc", test.DeltaTc);
        cmd.Parameters.AddWithValue("$ftf1", test.FinalTf1);
        cmd.Parameters.AddWithValue("$ftf2", test.FinalTf2);
        cmd.Parameters.AddWithValue("$fts", test.FinalTs);
        cmd.Parameters.AddWithValue("$ftc", test.FinalTc);
        cmd.Parameters.AddWithValue("$ftf1t", test.FinalTf1Time);
        cmd.Parameters.AddWithValue("$ftf2t", test.FinalTf2Time);
        cmd.Parameters.AddWithValue("$ftst", test.FinalTsTime);
        cmd.Parameters.AddWithValue("$ftct", test.FinalTcTime);
        cmd.Parameters.AddWithValue("$mtf1", test.MaxTf1);
        cmd.Parameters.AddWithValue("$mtf2", test.MaxTf2);
        cmd.Parameters.AddWithValue("$mts", test.MaxTs);
        cmd.Parameters.AddWithValue("$mtc", test.MaxTc);
        cmd.Parameters.AddWithValue("$mtf1t", test.MaxTf1Time);
        cmd.Parameters.AddWithValue("$mtf2t", test.MaxTf2Time);
        cmd.Parameters.AddWithValue("$mtst", test.MaxTsTime);
        cmd.Parameters.AddWithValue("$mtct", test.MaxTcTime);
        cmd.Parameters.AddWithValue("$time", test.TotalTestTime);
        cmd.Parameters.AddWithValue("$cp", test.ConstPower);
        cmd.Parameters.AddWithValue("$pheno", test.PhenoCode);
        cmd.Parameters.AddWithValue("$ftime", test.FlameTime);
        cmd.Parameters.AddWithValue("$fdur", test.FlameDuration);
        cmd.Parameters.AddWithValue("$memo", (object?)test.Memo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pid", test.ProductId);
        cmd.Parameters.AddWithValue("$tid", test.TestId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 查询试验记录
    /// </summary>
    public TestMaster? GetTest(string productId, string testId)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM testmaster WHERE productid=$pid AND testid=$tid";
        cmd.Parameters.AddWithValue("$pid", productId);
        cmd.Parameters.AddWithValue("$tid", testId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadTest(reader);
        return null;
    }

    /// <summary>
    /// 查询试验历史列表
    /// </summary>
    public List<TestMaster> QueryTests(DateTime from, DateTime to, string? productId = null, string? operatorName = null)
    {
        var result = new List<TestMaster>();
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();

        var where = new List<string>();
        where.Add("testdate BETWEEN $from AND $to");
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));

        if (!string.IsNullOrEmpty(productId))
        {
            where.Add("productid LIKE '%' || $pid || '%'");
            cmd.Parameters.AddWithValue("$pid", productId);
        }
        if (!string.IsNullOrEmpty(operatorName))
        {
            where.Add("operator = $op");
            cmd.Parameters.AddWithValue("$op", operatorName);
        }

        cmd.CommandText = $"SELECT * FROM testmaster WHERE {string.Join(" AND ", where)} ORDER BY testdate DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadTest(reader));
        return result;
    }

    /// <summary>
    /// 获取所有操作员列表（下拉选择用）
    /// </summary>
    public List<string> GetOperatorNames()
    {
        var result = new List<string>();
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT username FROM operators ORDER BY username";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    private TestMaster ReadTest(SqliteDataReader reader)
    {
        return new TestMaster
        {
            ProductId = reader.GetString(reader.GetOrdinal("productid")),
            TestId = reader.GetString(reader.GetOrdinal("testid")),
            TestDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("testdate"))),
            AmbTemp = reader.GetDouble(reader.GetOrdinal("ambtemp")),
            AmbHumi = reader.GetDouble(reader.GetOrdinal("ambhumi")),
            According = reader.GetString(reader.GetOrdinal("according")),
            Operator = reader.GetString(reader.GetOrdinal("operator")),
            ApparatusId = reader.GetString(reader.GetOrdinal("apparatusid")),
            ApparatusName = reader.GetString(reader.GetOrdinal("apparatusname")),
            ApparatusChkDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("apparatuschkdate"))),
            RptNo = reader.GetString(reader.GetOrdinal("rptno")),
            PreWeight = reader.GetDouble(reader.GetOrdinal("preweight")),
            PostWeight = reader.GetDouble(reader.GetOrdinal("postweight")),
            LostWeight = reader.GetDouble(reader.GetOrdinal("lostweight")),
            LostWeightPer = reader.GetDouble(reader.GetOrdinal("lostweight_per")),
            TotalTestTime = reader.GetInt32(reader.GetOrdinal("totaltesttime")),
            ConstPower = reader.GetInt32(reader.GetOrdinal("constpower")),
            PhenoCode = reader.GetString(reader.GetOrdinal("phenocode")),
            FlameTime = reader.GetInt32(reader.GetOrdinal("flametime")),
            FlameDuration = reader.GetInt32(reader.GetOrdinal("flameduration")),
            MaxTf1 = reader.GetDouble(reader.GetOrdinal("maxtf1")),
            MaxTf2 = reader.GetDouble(reader.GetOrdinal("maxtf2")),
            MaxTs = reader.GetDouble(reader.GetOrdinal("maxts")),
            MaxTc = reader.GetDouble(reader.GetOrdinal("maxtc")),
            MaxTf1Time = reader.GetInt32(reader.GetOrdinal("maxtf1_time")),
            MaxTf2Time = reader.GetInt32(reader.GetOrdinal("maxtf2_time")),
            MaxTsTime = reader.GetInt32(reader.GetOrdinal("maxts_time")),
            MaxTcTime = reader.GetInt32(reader.GetOrdinal("maxtc_time")),
            FinalTf1 = reader.GetDouble(reader.GetOrdinal("finaltf1")),
            FinalTf2 = reader.GetDouble(reader.GetOrdinal("finaltf2")),
            FinalTs = reader.GetDouble(reader.GetOrdinal("finalts")),
            FinalTc = reader.GetDouble(reader.GetOrdinal("finaltc")),
            FinalTf1Time = reader.GetInt32(reader.GetOrdinal("finaltf1_time")),
            FinalTf2Time = reader.GetInt32(reader.GetOrdinal("finaltf2_time")),
            FinalTsTime = reader.GetInt32(reader.GetOrdinal("finalts_time")),
            FinalTcTime = reader.GetInt32(reader.GetOrdinal("finaltc_time")),
            DeltaTf1 = reader.GetDouble(reader.GetOrdinal("deltatf1")),
            DeltaTf2 = reader.GetDouble(reader.GetOrdinal("deltatf2")),
            DeltaTf = reader.GetDouble(reader.GetOrdinal("deltatf")),
            DeltaTs = reader.GetDouble(reader.GetOrdinal("deltats")),
            DeltaTc = reader.GetDouble(reader.GetOrdinal("deltatc")),
            Memo = reader.IsDBNull(reader.GetOrdinal("memo")) ? null : reader.GetString(reader.GetOrdinal("memo")),
            Flag = reader.IsDBNull(reader.GetOrdinal("flag")) ? null : reader.GetString(reader.GetOrdinal("flag"))
        };
    }

    #endregion

    #region 传感器操作

    /// <summary>
    /// 获取所有传感器配置
    /// </summary>
    public List<Sensor> GetAllSensors()
    {
        var result = new List<Sensor>();
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sensors ORDER BY sensorid";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadSensor(reader));
        return result;
    }

    /// <summary>
    /// 更新传感器输出值
    /// </summary>
    public void UpdateSensorValue(int sensorId, double outputValue, double inputValue)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sensors SET outputvalue=$ov, inputvalue=$iv WHERE sensorid=$sid";
        cmd.Parameters.AddWithValue("$ov", outputValue);
        cmd.Parameters.AddWithValue("$iv", inputValue);
        cmd.Parameters.AddWithValue("$sid", sensorId);
        cmd.ExecuteNonQuery();
    }

    private Sensor ReadSensor(SqliteDataReader reader)
    {
        return new Sensor
        {
            SensorId = reader.GetInt32(reader.GetOrdinal("sensorid")),
            SensorName = reader.GetString(reader.GetOrdinal("sensorname")),
            DispName = reader.GetString(reader.GetOrdinal("dispname")),
            SensorGroup = reader.GetString(reader.GetOrdinal("sensorgroup")),
            Unit = reader.GetString(reader.GetOrdinal("unit")),
            Discription = reader.GetString(reader.GetOrdinal("discription")),
            Flag = reader.GetString(reader.GetOrdinal("flag")),
            SignalZero = reader.GetDouble(reader.GetOrdinal("signalzero")),
            SignalSpan = reader.GetDouble(reader.GetOrdinal("signalspan")),
            OutputZero = reader.GetDouble(reader.GetOrdinal("outputzero")),
            OutputSpan = reader.GetDouble(reader.GetOrdinal("outputspan")),
            OutputValue = reader.GetDouble(reader.GetOrdinal("outputvalue")),
            InputValue = reader.GetDouble(reader.GetOrdinal("inputvalue")),
            SignalType = reader.GetInt32(reader.GetOrdinal("signaltype"))
        };
    }

    #endregion

    #region 校准记录操作

    /// <summary>
    /// 保存校准记录
    /// </summary>
    public void SaveCalibrationRecord(CalibrationRecord record)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO CalibrationRecords
                (Id, CalibrationDate, CalibrationType, ApparatusId, Operator,
                 TemperatureData, UniformityResult, MaxDeviation, AverageTemperature,
                 PassedCriteria, Remarks, CreatedAt,
                 TempA1, TempA2, TempA3, TempB1, TempB2, TempB3, TempC1, TempC2, TempC3,
                 TAvg, TAvgAxis1, TAvgAxis2, TAvgAxis3, TAvgLevela, TAvgLevelb, TAvgLevelc,
                 TDevAxis1, TDevAxis2, TDevAxis3, TDevLevela, TDevLevelb, TDevLevelc,
                 TAvgDevAxis, TAvgDevLevel, CenterTempData, Memo)
            VALUES
                ($id, $date, $type, $aid, $op,
                 $tempdata, $ur, $md, $at,
                 $pc, $remarks, $created,
                 $ta1, $ta2, $ta3, $tb1, $tb2, $tb3, $tc1, $tc2, $tc3,
                 $tavg, $tavga1, $tavga2, $tavga3, $tavgl1, $tavgl2, $tavgl3,
                 $tdeva1, $tdeva2, $tdeva3, $tdevl1, $tdevl2, $tdevl3,
                 $tavgdva, $tavgdvl, $ctd, $memo)";
        cmd.Parameters.AddWithValue("$id", record.Id);
        cmd.Parameters.AddWithValue("$date", record.CalibrationDate);
        cmd.Parameters.AddWithValue("$type", record.CalibrationType);
        cmd.Parameters.AddWithValue("$aid", record.ApparatusId);
        cmd.Parameters.AddWithValue("$op", record.Operator);
        cmd.Parameters.AddWithValue("$tempdata", record.TemperatureData);
        cmd.Parameters.AddWithValue("$ur", (object?)record.UniformityResult ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$md", (object?)record.MaxDeviation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$at", (object?)record.AverageTemperature ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pc", record.PassedCriteria);
        cmd.Parameters.AddWithValue("$remarks", record.Remarks);
        cmd.Parameters.AddWithValue("$created", record.CreatedAt);
        cmd.Parameters.AddWithValue("$ta1", (object?)record.TempA1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ta2", (object?)record.TempA2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ta3", (object?)record.TempA3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tb1", (object?)record.TempB1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tb2", (object?)record.TempB2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tb3", (object?)record.TempB3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tc1", (object?)record.TempC1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tc2", (object?)record.TempC2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tc3", (object?)record.TempC3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavg", (object?)record.TAvg ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavga1", (object?)record.TAvgAxis1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavga2", (object?)record.TAvgAxis2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavga3", (object?)record.TAvgAxis3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavgl1", (object?)record.TAvgLevela ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavgl2", (object?)record.TAvgLevelb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavgl3", (object?)record.TAvgLevelc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdeva1", (object?)record.TDevAxis1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdeva2", (object?)record.TDevAxis2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdeva3", (object?)record.TDevAxis3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdevl1", (object?)record.TDevLevela ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdevl2", (object?)record.TDevLevelb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tdevl3", (object?)record.TDevLevelc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavgdva", (object?)record.TAvgDevAxis ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tavgdvl", (object?)record.TAvgDevLevel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ctd", (object?)record.CenterTempData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$memo", (object?)record.Memo ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取校准历史记录
    /// </summary>
    public List<CalibrationRecord> GetCalibrationRecords(int? apparatusId = null)
    {
        var result = new List<CalibrationRecord>();
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        if (apparatusId.HasValue)
        {
            cmd.CommandText = "SELECT * FROM CalibrationRecords WHERE ApparatusId=$aid ORDER BY CalibrationDate DESC";
            cmd.Parameters.AddWithValue("$aid", apparatusId.Value);
        }
        else
        {
            cmd.CommandText = "SELECT * FROM CalibrationRecords ORDER BY CalibrationDate DESC";
        }
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadCalibrationRecord(reader));
        return result;
    }

    private CalibrationRecord ReadCalibrationRecord(SqliteDataReader reader)
    {
        double? ReadNullableDouble(string col)
        {
            var ord = reader.GetOrdinal(col);
            return reader.IsDBNull(ord) ? null : reader.GetDouble(ord);
        }
        string? ReadNullableString(string col)
        {
            var ord = reader.GetOrdinal(col);
            return reader.IsDBNull(ord) ? null : reader.GetString(ord);
        }

        return new CalibrationRecord
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            CalibrationDate = reader.GetString(reader.GetOrdinal("CalibrationDate")),
            CalibrationType = reader.GetString(reader.GetOrdinal("CalibrationType")),
            ApparatusId = reader.GetInt32(reader.GetOrdinal("ApparatusId")),
            Operator = reader.GetString(reader.GetOrdinal("Operator")),
            TemperatureData = reader.GetString(reader.GetOrdinal("TemperatureData")),
            UniformityResult = ReadNullableDouble("UniformityResult"),
            MaxDeviation = ReadNullableDouble("MaxDeviation"),
            AverageTemperature = ReadNullableDouble("AverageTemperature"),
            PassedCriteria = reader.GetInt32(reader.GetOrdinal("PassedCriteria")),
            Remarks = reader.GetString(reader.GetOrdinal("Remarks")),
            CreatedAt = reader.GetString(reader.GetOrdinal("CreatedAt")),
            TempA1 = ReadNullableDouble("TempA1"), TempA2 = ReadNullableDouble("TempA2"), TempA3 = ReadNullableDouble("TempA3"),
            TempB1 = ReadNullableDouble("TempB1"), TempB2 = ReadNullableDouble("TempB2"), TempB3 = ReadNullableDouble("TempB3"),
            TempC1 = ReadNullableDouble("TempC1"), TempC2 = ReadNullableDouble("TempC2"), TempC3 = ReadNullableDouble("TempC3"),
            TAvg = ReadNullableDouble("TAvg"),
            TAvgAxis1 = ReadNullableDouble("TAvgAxis1"), TAvgAxis2 = ReadNullableDouble("TAvgAxis2"), TAvgAxis3 = ReadNullableDouble("TAvgAxis3"),
            TAvgLevela = ReadNullableDouble("TAvgLevela"), TAvgLevelb = ReadNullableDouble("TAvgLevelb"), TAvgLevelc = ReadNullableDouble("TAvgLevelc"),
            TDevAxis1 = ReadNullableDouble("TDevAxis1"), TDevAxis2 = ReadNullableDouble("TDevAxis2"), TDevAxis3 = ReadNullableDouble("TDevAxis3"),
            TDevLevela = ReadNullableDouble("TDevLevela"), TDevLevelb = ReadNullableDouble("TDevLevelb"), TDevLevelc = ReadNullableDouble("TDevLevelc"),
            TAvgDevAxis = ReadNullableDouble("TAvgDevAxis"), TAvgDevLevel = ReadNullableDouble("TAvgDevLevel"),
            CenterTempData = ReadNullableString("CenterTempData"),
            Memo = ReadNullableString("Memo")
        };
    }

    #endregion
}
