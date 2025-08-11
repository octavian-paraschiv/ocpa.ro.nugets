using System;

namespace ThorusCommon.IO;

public class MeteoDataEntry
{
    public sbyte Dbi { get; set; }

    public short R { get; set; }

    public short C { get; set; }

    public string RegionCode { get; set; }

    public DateTime Timestamp { get; set; }

    public int C_00 { get; set; }

    public int F_SI { get; set; }

    public int L_00 { get; set; }

    public int N_00 { get; set; }

    public int N_DD { get; set; }

    public int P_00 { get; set; }

    public int P_01 { get; set; }

    public int R_00 { get; set; }

    public int R_DD { get; set; }

    public int T_01 { get; set; }

    public int T_NH { get; set; }

    public int T_NL { get; set; }

    public int T_SH { get; set; }

    public int T_SL { get; set; }

    public int T_TE { get; set; }

    public int T_TS { get; set; }

    public int W_00 { get; set; }

    public int W_01 { get; set; }

    public int W_10 { get; set; }

    public int W_11 { get; set; }
}