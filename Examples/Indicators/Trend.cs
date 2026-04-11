using System;
using System.Drawing;
using Runtime.Script;
using TradeApi.History;
using TradeApi.Indicators;

namespace Trend
{
    #region (ADX)Average Directional Index
    public class ADX : IndicatorBuilder
    {
        public ADX()
            : base()
        {
            Credentials.ProjectName = "Average Directional Index";

            Lines.Set("ADX");
            Lines["ADX"].Color = Color.Green;

            Lines.Set("PlusDI");
            Lines["PlusDI"].Color = Color.Blue;

            Lines.Set("MinusDI");
            Lines["MinusDI"].Color = Color.Red;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Type of Moving Average", 0)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of Moving Average", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 14;

        private BuiltInIndicator smoothedDmPlus;
        private BuiltInIndicator smoothedDmMinus;
        private BuiltInIndicator smoothedTrueRange;
        private BuiltInIndicator smoothedDx;

        public override void Init()
        {
            ScriptShortName = string.Format("ADX({0})", MAPeriod);

            Lines["ADX"].SetLineBegin(2*MAPeriod - 1);
            Lines["PlusDI"].SetLineBegin(MAPeriod);
            Lines["MinusDI"].SetLineBegin(MAPeriod);

            smoothedTrueRange = IndicatorsManager.BuildIn.MA(
                (index) =>
                {
                    return getTrueRange(index);
                },
                MAPeriod,
                MAType, 
                0,
                1);

            smoothedDmPlus = IndicatorsManager.BuildIn.MA(
                (index) =>
                {
                    double plusDM, minusDM;
                    GetDmPlusMinus(index, out plusDM, out minusDM);
                    return plusDM;
                },
                MAPeriod,
                MAType,
                0,
                1);

            smoothedDmMinus = IndicatorsManager.BuildIn.MA(
                (index) =>
                {
                    double plusDM, minusDM;
                    GetDmPlusMinus(index, out plusDM, out minusDM);
                    return minusDM;
                },
                MAPeriod,
                MAType,
                0,
                1);

            smoothedDx = IndicatorsManager.BuildIn.MA(GetDx, MAPeriod, MAType, 0, MAPeriod);
        }

        private double getTrueRange(int index)
        {
            double hi = HistoryDataSeries.GetValue(PriceType.High, index);
            double lo = HistoryDataSeries.GetValue(PriceType.Low, index);
            double prevClose = (HistoryDataSeries.Count > index + 1) ? HistoryDataSeries.GetValue(PriceType.Close, index + 1) : HistoryDataSeries.GetValue(PriceType.Close, index);
            return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
        }

        private void GetDmPlusMinus(int index, out double plusDM, out double minusDM)
        {
            if (index + 1 >= HistoryDataSeries.Count)
            {
                plusDM = 0D;
                minusDM = 0D;
                return;
            }
            double upMove = HistoryDataSeries.GetValue(PriceType.High, index) - HistoryDataSeries.GetValue(PriceType.High, index + 1);
            double downMove = HistoryDataSeries.GetValue(PriceType.Low, index + 1) - HistoryDataSeries.GetValue(PriceType.Low, index);

            plusDM = 0.0;
            if (upMove > downMove && upMove > 0)
            {
                plusDM = upMove;
            }

            minusDM = 0.0;
            if (downMove > upMove && downMove > 0) 
            {
                minusDM = downMove;
            }
        }

        private double GetDiPlus(int index)
        {
            double smoothedTr = smoothedTrueRange.GetValue(index);
            if (smoothedTr == 0)
                return 0.0;
           return 100 * smoothedDmPlus.GetValue(index) / smoothedTr;
        }

        private double GetDiMinus(int index)
        {
            double smoothedTr = smoothedTrueRange.GetValue(index);
            if (smoothedTr == 0)
                return 0.0;
            return 100 * smoothedDmMinus.GetValue(index) / smoothedTr;
        }

        private double GetDx(int index)
        {
            double diPlus = GetDiPlus(index);
            double diMinus = GetDiMinus(index);
            double divider = diPlus + diMinus;
            if (divider == 0)
                return 0.0;

            return Math.Abs((diPlus - diMinus)/divider) * 100;
        }

        public override void Update(TickStatus args)
        {
            Lines["ADX"].SetValue(smoothedDx.GetValue());
            Lines["PlusDI"].SetValue(GetDiPlus(0));
            Lines["MinusDI"].SetValue(GetDiMinus(0));
        }
    }
    #endregion

    #region (ALLIGATOR)Alligator
    public class ALLIGATOR : IndicatorBuilder
    {
        public ALLIGATOR()
            : base()
        {
            Credentials.ProjectName = "Alligator";

            Lines.Set("JAW");
            Lines["JAW"].Color = Color.Blue;

            Lines.Set("TEETH");
            Lines["TEETH"].Color = Color.Red;

            Lines.Set("LIPS");
            Lines["LIPS"].Color = Color.Green;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Combobox, "Type of Jaw Moving Average", 0)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode JawMAType = MAMode.SMA;

        [InputParameter(InputType.Combobox, "Sources prices for Jaw MA", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType JawSourcePrice = PriceType.Close;

        [InputParameter(InputType.Numeric, "Period of Jaw MA", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int JawMAPeriod = 13;

        [InputParameter(InputType.Numeric, "Shift of Jaw MA", 3)]
        [SimpleNumeric(1D, 99999D)]
        public int JawMAShift = 8;

        [InputParameter(InputType.Combobox, "Type of Jaw MA", 4)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode TeethMAType = MAMode.LWMA;

        [InputParameter(InputType.Combobox, "Sources prices for Teeth MA", 5)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType TeethSourcePrice = PriceType.Close;

        [InputParameter(InputType.Numeric, "Period of Teeth MA", 6)]
        [SimpleNumeric(1D, 99999D)]
        public int TeethMAPeiod = 8;

        [InputParameter(InputType.Numeric, "Shift of Teeth MA", 7)]
        [SimpleNumeric(1D, 99999D)]
        public int TeethMAShift = 5;

        [InputParameter(InputType.Combobox, "Type of Lips MA", 8)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode LipsMAType = MAMode.SMA;

        [InputParameter(InputType.Combobox, "Sources prices for Lips MA", 9)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType LipsSourcePrice = PriceType.Close;

        [InputParameter(InputType.Numeric, "Period of Lips Moving Average", 10)]
        [SimpleNumeric(1D, 99999D)]
        public int LipsMAPeiod = 5;

        [InputParameter(InputType.Numeric, "Shift of Lips Moving Average", 11)]
        [SimpleNumeric(1D, 99999D)]
        public int LipsMAShift = 3;

        private BuiltInIndicator jawMa;
        private BuiltInIndicator teethMa;
        private BuiltInIndicator lipsMa;

        public override void Init()
        {
            ScriptShortName = string.Format("ALLIGATOR({0};{1};{2})", JawMAPeriod, TeethMAPeiod, LipsMAPeiod);

            Lines[0].TimeShift = JawMAShift;
            Lines[1].TimeShift = TeethMAShift;
            Lines[2].TimeShift = LipsMAShift;

            jawMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, JawMAPeriod, JawMAType, 0, JawSourcePrice);
            teethMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, TeethMAPeiod, TeethMAType, 0, TeethSourcePrice);
            lipsMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, LipsMAPeiod, LipsMAType, 0, LipsSourcePrice);
        }

        public override void Update(TickStatus args)
        {
            Lines["JAW"].SetValue(jawMa.GetValue());
            Lines["TEETH"].SetValue(teethMa.GetValue());
            Lines["LIPS"].SetValue(lipsMa.GetValue());
        }
    }
    #endregion

    #region (DMI)Directional Movement Index
    public class DMI : IndicatorBuilder
    {
        public DMI()
            : base()
        {
            Credentials.ProjectName = "Directional Movement Index";

            Lines.Set("PlusMA");
            Lines["PlusMA"].Color = Color.Blue;

            Lines.Set("MinusMA");
            Lines["MinusMA"].Color = Color.Red;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Type of Moving Average", 0)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of Moving Average", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 14;

        private BuiltInIndicator trueRangeMa;
        private BuiltInIndicator minusMa;
        private BuiltInIndicator plusMa;

        public override void Init()
        {
            ScriptShortName = string.Format("DMI({0})", MAPeriod);

            Lines["PlusMA"].SetLineBegin(MAPeriod);
            Lines["MinusMA"].SetLineBegin(MAPeriod);

            trueRangeMa = IndicatorsManager.BuildIn.MA(getTrueRange, MAPeriod, MAType);

            plusMa = IndicatorsManager.BuildIn.MA(
                (index) =>
                {
                    double plusDM, minusDM;
                    GetPlusMinus(index, out plusDM, out minusDM);
                    return plusDM;
                },
                MAPeriod,
                MAType);

            minusMa = IndicatorsManager.BuildIn.MA(
                (index) =>
                {
                    double plusDM, minusDM;
                    GetPlusMinus(index, out plusDM, out minusDM);
                    return minusDM;
                },
                MAPeriod,
                MAType);
        }

        private double getTrueRange(int index)
        {
            double hi = HistoryDataSeries.GetValue(PriceType.High, index);
            double lo = HistoryDataSeries.GetValue(PriceType.Low, index);
            double prevClose = (HistoryDataSeries.Count > index + 1) ? HistoryDataSeries.GetValue(PriceType.Close, index + 1) : HistoryDataSeries.GetValue(PriceType.Close, index);

            return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
        }

        private void GetPlusMinus(int index, out double plusDM, out double minusDM)
        {
            double smoothedTR = trueRangeMa.GetValue();
            if (smoothedTR == 0 || index + 1 >= HistoryDataSeries.Count)
            {
                plusDM = 0D;
                minusDM = 0D;
                return;
            }
            plusDM = HistoryDataSeries.GetValue(PriceType.High, index) - HistoryDataSeries.GetValue(PriceType.High, index + 1);
            if (plusDM < 0.0)
                plusDM = 0.0;
            else
                plusDM *= 100D / smoothedTR;
            minusDM = HistoryDataSeries.GetValue(PriceType.Low, index + 1) - HistoryDataSeries.GetValue(PriceType.Low, index);
            if (minusDM < 0.0)
                minusDM = 0.0;
            else
                minusDM *= 100D / smoothedTR;

            if (plusDM > minusDM)
                minusDM = 0.0;
            else
                plusDM = 0.0;
        }

        public override void Update(TickStatus args)
        {
            Lines["PlusMA"].SetValue(plusMa.GetValue());
            Lines["MinusMA"].SetValue(minusMa.GetValue());
        }
    }
    #endregion

    #region (ICH)Ichimoku
    public class ICH : IndicatorBuilder
    {
        public ICH()
            : base()
        {
            Credentials.ProjectName = "Ichimoku";

            Lines.Set("TENKAN");

            Lines.Set("KIJUN");
            Lines["KIJUN"].Color = Color.Lime;

            Lines.Set("SENKOU_SPANA");
            Lines["SENKOU_SPANA"].Color = Color.SpringGreen;

            Lines.Set("SENKOU_SPANB");
            Lines["SENKOU_SPANB"].Color = Color.Red;

            Lines.Set("CHINKOU_SPAN");
            Lines["CHINKOU_SPAN"].Color = Color.FromArgb(255, 153, 0);

            Clouds.Set("ICH_CLOUD", GradientMethod, Lines["SENKOU_SPANA"].Color, Lines["SENKOU_SPANB"].Color);

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Tenkan Sen", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int TenkanPeriod = 9;

        [InputParameter(InputType.Numeric, "Kijun Sen", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int KijunPeriod = 26;

        [InputParameter(InputType.Numeric, "Senkou Span B", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int SenkouSpanB = 52;

        [InputParameter(InputType.Combobox, "Gradient method", 3)]
        [ComboboxItem("Simple", GradientMode.Simple)]
        [ComboboxItem("Linear", GradientMode.Linear)]
        [ComboboxItem("None", GradientMode.None)]
        public GradientMode GradientMethod = GradientMode.Simple;

        public override void Init()
        {
            Lines["SENKOU_SPANA"].TimeShift = KijunPeriod;
            Lines["SENKOU_SPANB"].TimeShift = KijunPeriod;
            Clouds["ICH_CLOUD"].TimeShift = KijunPeriod;
            Clouds["ICH_CLOUD"].GradientMode = GradientMethod;

            ScriptShortName = string.Format("ICH({0};{1};{2})", TenkanPeriod, KijunPeriod, SenkouSpanB);
        }

        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
                return;

            int count = HistoryDataSeries.Count;
            if (count < TenkanPeriod || count < KijunPeriod || count < SenkouSpanB)
                return;

            Lines["TENKAN"].SetValue(GetAverage(TenkanPeriod, 0));

            Lines["KIJUN"].SetValue(GetAverage(KijunPeriod, 0));

            Lines["CHINKOU_SPAN"].SetValue(HistoryDataSeries.GetValue(PriceType.Close), KijunPeriod);

            double senkouSpanA = (Lines["TENKAN"].GetValue() + Lines["KIJUN"].GetValue()) / 2;
            Lines["SENKOU_SPANA"].SetValue(senkouSpanA);

            double senkouSpanB = GetAverage(SenkouSpanB, 0);
            Lines["SENKOU_SPANB"].SetValue(senkouSpanB);

            Clouds["ICH_CLOUD"].Set(senkouSpanA, senkouSpanB);
        }

        public double GetAverage(int period, int offset)
        {
            double high = HistoryDataSeries.GetValue(PriceType.High, offset);
            double low = HistoryDataSeries.GetValue(PriceType.Low, offset);
            double price = 0.0;
            for (int i = offset + 1; i < offset + period; i++)
            {
                price = HistoryDataSeries.GetValue(PriceType.High, i);
                if (high < price)
                    high = price;
                price = HistoryDataSeries.GetValue(PriceType.Low, i);
                if (low > price)
                    low = price;
            }
            return (high + low) / 2.0;
        }
    }
    #endregion

    #region (SAR)Parabolic Time/Price System
    public class SAR : IndicatorBuilder
    {
        public SAR()
            : base()
        {
            Credentials.ProjectName = "Parabolic Time/Price System";

            Lines.Set("SAR");
            Lines["SAR"].Color = Color.Firebrick;
            Lines["SAR"].Width = 4;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Step of parabolic SAR system", 0)]
        [SimpleNumeric(2, 0.01)]
        public double Step = 0.02;

        [InputParameter(InputType.Numeric, "Maximum", 1)]
        [SimpleNumeric(2, 0.01)]
        public double Maximum = 0.2;

        private bool first;
        private bool dirlong;
        private double start, last_high, last_low;
        private double ep, sar, price_low, price_high;

        public override void Init()
        {
            ScriptShortName = "SAR";
            first = true;
            dirlong = true;
            start = Step;
            last_high = -10000000.0;
            last_low = 10000000.0;
            sar = 0;
        }

        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
                return;

            if (HistoryDataSeries.Count == 1)
                return;

            if (first)
            {
                price_low = HistoryDataSeries.GetValue(PriceType.Low);
                price_high = HistoryDataSeries.GetValue(PriceType.High);
                if (last_low > price_low)
                    last_low = price_low;
                if (last_high < price_high)
                    last_high = price_high;

                double prev_low = HistoryDataSeries.GetValue(PriceType.Low, 1);
                double prev_high = HistoryDataSeries.GetValue(PriceType.High, 1);

                if (price_high > prev_high && price_low > prev_low)
                {
                    Lines["SAR"].SetValue(prev_low);
                    first = false;
                    return;
                }
                if (price_high < prev_high && price_low < prev_low)
                {
                    dirlong = false;
                    Lines["SAR"].SetValue(prev_high);
                    first = false;
                    return;
                }
            }

            price_low = HistoryDataSeries.GetValue(PriceType.Low);
            price_high = HistoryDataSeries.GetValue(PriceType.High);
            double price = Lines["SAR"].GetValue(1);

            if (dirlong && price_low < price)
            {
                start = Step; dirlong = false;
                ep = price_low;
                last_low = price_low;
                Lines["SAR"].SetValue(last_high);
                return;
            }

            if (!dirlong && price_high > price)
            {
                start = Step; dirlong = true;
                ep = price_high;
                last_high = price_high;
                Lines["SAR"].SetValue(last_low);
                return;
            }

            sar = price + start * (ep - price);

            if (dirlong)
            {
                if (ep < price_high && (start + Step) <= Maximum)
                    start += Step;
                if (price_high < HistoryDataSeries.GetValue(PriceType.High, 1) && HistoryDataSeries.Count == 2)
                    sar = price;
                if (sar > HistoryDataSeries.GetValue(PriceType.Low, 1)) sar = HistoryDataSeries.GetValue(PriceType.Low, 1);
                if (sar > HistoryDataSeries.GetValue(PriceType.Low, 2)) sar = HistoryDataSeries.GetValue(PriceType.Low, 2);
                if (sar > price_low)
                {
                    start = Step; dirlong = false; ep = price_low;
                    last_low = price_low;
                    Lines["SAR"].SetValue(last_high);
                    return;
                }
                if (ep < price_high) { last_high = price_high; ep = price_high; }
            }
            else
            {
                if (ep > price_low && (start + Step) <= Maximum)
                    start += Step;
                if (price_low < HistoryDataSeries.GetValue(PriceType.Low, 1) && HistoryDataSeries.Count == 2)
                    sar = price;
                if (sar < HistoryDataSeries.GetValue(PriceType.High, 1)) sar = HistoryDataSeries.GetValue(PriceType.High, 1);
                if (sar < HistoryDataSeries.GetValue(PriceType.High, 2)) sar = HistoryDataSeries.GetValue(PriceType.High, 2);
                if (sar < price_high)
                {
                    start = Step; dirlong = true; ep = price_high;
                    last_high = price_high;
                    Lines["SAR"].SetValue(last_low);
                    return;
                }
                if (ep > price_low) { last_low = price_low; ep = price_low; }
            }
            Lines["SAR"].SetValue(sar);
        }
    }
    #endregion

    #region (SI)Swing Index
    public class SI : IndicatorBuilder
    {
        public SI()
            : base()
        {
            Credentials.ProjectName = "Swing Index";

            Lines.Set("SI");
            Lines["SI"].Color = Color.FromArgb(0, 102, 252);

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "T")]
        [SimpleNumeric(1D, 99999D)]
        public double T = 300.0;

        private BuiltInIndicator atrIndicator;

        public override void Init()
        {
            ScriptShortName = string.Format("SI({0})", T);
            atrIndicator = IndicatorsManager.BuildIn.ATR(HistoryDataSeries, 1);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < 2)
            {
                Lines["SI"].SetValue(0);
                return;
            }

            double prevOpen = HistoryDataSeries.GetValue(PriceType.Open, 1);
            double prevClose = HistoryDataSeries.GetValue(PriceType.Close, 1);
            double open = HistoryDataSeries.GetValue(PriceType.Open);
            double high = HistoryDataSeries.GetValue(PriceType.High);
            double low = HistoryDataSeries.GetValue(PriceType.Low);
            double close = HistoryDataSeries.GetValue(PriceType.Close);

            double ER = 0;
            if (prevClose >= low && prevClose <= high)
                ER = 0;
            else
            {
                if (prevClose > high)
                    ER = Math.Abs(high - prevClose);
                if (prevClose < low)
                    ER = Math.Abs(low - prevClose);
            }

            double K = Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose));
            double SH = Math.Abs(prevClose - prevOpen);
            double R = atrIndicator.GetValue() - 0.5 * ER + 0.25 * SH;

            double si = 0;
            if (Math.Abs(R) >= 0.000001)
                si = 50 * (close - prevClose + 0.5 * (close - open) + 0.25 * (prevClose - prevOpen)) * K / (T * 0.0001/*Instruments.Current.TickSize*/) / R; //TODO:hsa - not implemented

            Lines["SI"].SetValue(Lines["SI"].GetValue(1) + si);
        }
    }
    #endregion

    #region (ZZ)ZigZag
    public class ZZ : IndicatorBuilder
    {
        public ZZ()
            : base()
        {
            Credentials.ProjectName = "ZigZag";

            Lines.Set("UpTrendLine");
            Lines["UpTrendLine"].Color = Color.Yellow;
            Lines["UpTrendLine"].Width = 2;

            Lines.Set("DownTrendLine");
            Lines["DownTrendLine"].Color = Color.Yellow;
            Lines["DownTrendLine"].Width = 2;

            base.SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Percent Deviation")]
        [SimpleNumeric(0D, 99999D, 2, 0.01D)]
        public double deviation = 1;

        private int trendLineLenght;
        private int retracementLenght;
        private int direction;
        private double lastTurnPoint;

        public override void Init()
        {
            ScriptShortName = string.Format("ZigZag({0})", deviation);

            trendLineLenght = 0;
            retracementLenght = 0;
            direction = 1;
            lastTurnPoint = 0;
        }

        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
            {
                trendLineLenght++;
                retracementLenght++;
                return;
            }

            if (HistoryDataSeries.Count == 0)
                return;

            double high = High(0);
            double low = Low(0);

            if (direction == 1)
            {
                if (high >= lastTurnPoint)
                {
                    lastTurnPoint = high;
                    retracementLenght = 0;
                    UpTrendLine(trendLineLenght + 1);
                    return;
                }

                if (low <= lastTurnPoint - lastTurnPoint * deviation / 100)
                {
                    lastTurnPoint = low;
                    direction = -1;
                    trendLineLenght = retracementLenght;
                    retracementLenght = 0;
                    DownTrendLine(trendLineLenght + 1);
                    return;
                }
            }

            if (direction == -1)
            {
                if (low <= lastTurnPoint)
                {
                    lastTurnPoint = low;
                    retracementLenght = 0;
                    DownTrendLine(trendLineLenght + 1);
                    return;
                }

                if (high >= lastTurnPoint + lastTurnPoint * deviation / 100)
                {
                    lastTurnPoint = high;
                    direction = 1;
                    trendLineLenght = retracementLenght;
                    retracementLenght = 0;
                    UpTrendLine(trendLineLenght + 1);
                    return;
                }
            }
        }

        private void UpTrendLine(int x)
        {
            if (x > HistoryDataSeries.Count - 1)
                return;

            for (int i = 0; i < x; i++)
            {
                double y = High(0) - i * (High(0) - Low(x - 1)) / (x - 1);
                Lines["UpTrendLine"].SetValue(y, i);
            }
        }

        private void DownTrendLine(int x)
        {
            if (x > HistoryDataSeries.Count - 1)
                return;

            for (int i = 0; i < x; i++)
            {
                double y = Low(0) - i * (Low(0) - High(x - 1)) / (x - 1);
                Lines["DownTrendLine"].SetValue(y, i);
            }
        }

        private double High(int i)
        {
            return HistoryDataSeries.GetValue(PriceType.High, i);
        }

        private double Low(int i)
        {
            return HistoryDataSeries.GetValue(PriceType.Low, i);
        }
    }
    #endregion

    #region (PivotPoint) Pivot Point

    public class PivotPoint : IndicatorBuilder
    {
        [InputParameter(InputType.Checkbox, "Only current period", 0)]
        public bool OnlyCurrentPeriod = false;

        [InputParameter(InputType.Combobox, "Base period", 0)]
        [ComboboxItem("Hour", Period.Hour)]
        [ComboboxItem("Day", Period.Day)]
        [ComboboxItem("Week", Period.Week)]
        [ComboboxItem("Month", Period.Month)]
        public Period BasePeriod = Period.Hour;

        [InputParameter(InputType.Numeric, "Value", 2)]
        [SimpleNumeric(1D, 60D)]
        public int PeriodValue = 1;

        [InputParameter(InputType.Combobox, "Method", 0)]
        [ComboboxItem("Classic", CalculationMethod.Classic)]
        [ComboboxItem("Camarilla", CalculationMethod.Camarilla)]
        [ComboboxItem("Fibonacci", CalculationMethod.Fibonacci)]
        [ComboboxItem("Woodie", CalculationMethod.Woodie)]
        [ComboboxItem("DeMark", CalculationMethod.DeMark)]
        public CalculationMethod IndicatorCalculationMethod = CalculationMethod.Classic;

        HistoricalData LoadedHistory;
        HistoricalRequest hr;

        private bool isHistoryLoaded = false;
        private bool isAllowDataRange = false;
        private int lastIndicatorBarsCount = 0;

        public PivotPoint() : base()
        {
            #region Initialization
            Credentials.ProjectName = "Pivot Point";
            #endregion

            Lines.Set("PP");
            Lines["PP"].Color = Color.Black;
            Lines["PP"].Style = LineStyle.Horizontal;

            Lines.Set("R1");
            Lines["R1"].Color = Color.Red;
            Lines["R1"].Style = LineStyle.Horizontal;

            Lines.Set("R2");
            Lines["R2"].Color = Color.Red;
            Lines["R2"].Style = LineStyle.Horizontal;

            Lines.Set("R3");
            Lines["R3"].Color = Color.Red;
            Lines["R3"].Style = LineStyle.Horizontal;

            Lines.Set("S1");
            Lines["S1"].Color = Color.Blue;
            Lines["S1"].Style = LineStyle.Horizontal;

            Lines.Set("S2");
            Lines["S2"].Color = Color.Blue;
            Lines["S2"].Style = LineStyle.Horizontal;

            Lines.Set("S3");
            Lines["S3"].Color = Color.Blue;
            Lines["S3"].Style = LineStyle.Horizontal;

            base.SeparateWindow = true;
        }

        public override void Init()
        {
            if (this.HistoryDataSeries.HistoricalRequest is HeikinAshiHistoricalRequest)
                this.hr = new HeikinAshiHistoricalRequest(this.InstrumentsManager.Current, this.HistoryDataSeries.HistoricalRequest.DataType, this.BasePeriod, this.PeriodValue);
            else
                this.hr = new TimeHistoricalRequest(this.InstrumentsManager.Current, this.HistoryDataSeries.HistoricalRequest.DataType, this.BasePeriod, this.PeriodValue);
            this.HistoricalDataManager.OnLoaded = this.OnLoaded;
        }

        public override void Update(TickStatus args)
        {
            if (this.LoadedHistory == null)
                this.LoadPivotPointDataSeries();
            if (this.isHistoryLoaded)
                this.CalculateValues(0);
        }

        private void LoadPivotPointDataSeries()
        {
            Interval indicatorInterval = new Interval(this.HistoryDataSeries.GetTimeUtc(this.HistoryDataSeries.Count - 1), DateTime.UtcNow);
            this.LoadedHistory = this.HistoricalDataManager.Get(this.hr, indicatorInterval);
            this.isAllowDataRange = this.IsAllowIndicatorDataRange(indicatorInterval);
        }
        private void OnLoaded(HistoricalData historicalData)
        {
            for (int i = this.HistoryDataSeries.Count - 1; i >= 0; i--)
                this.CalculateValues(i);
            this.isHistoryLoaded = true;
        }

        void CalculateValues(int chartOffset)
        {
            if (!this.IsReadyToUpdate())
                return;

            int loadedHistoryOffset = this.LoadedHistory.FindInterval(this.HistoryDataSeries.GetTimeUtc(chartOffset)) + 1;
            if (this.OnlyCurrentPeriod && loadedHistoryOffset == 1 && this.LoadedHistory.Count > this.lastIndicatorBarsCount)
            {
                this.lastIndicatorBarsCount = this.LoadedHistory.Count;
                this.RemovePrevPeriodLines();
            }

            if (loadedHistoryOffset == this.LoadedHistory.Count || loadedHistoryOffset <= 0)
                return;

            if (this.OnlyCurrentPeriod && loadedHistoryOffset != 1)
                return;

            this.UpdateLines(loadedHistoryOffset, chartOffset);
        }

        private bool IsReadyToUpdate()
        {
            if (!this.isAllowDataRange)
                return false;

            if (!(this.HistoryDataSeries.HistoricalRequest is TimeHistoricalRequest) 
                && !(this.HistoryDataSeries.HistoricalRequest is HeikinAshiHistoricalRequest)
                && !(this.HistoryDataSeries.HistoricalRequest is ClusterHistoricalRequest))
                return false;
            if (!(this.LoadedHistory.HistoricalRequest is TimeHistoricalRequest) 
                && !(this.LoadedHistory.HistoricalRequest is HeikinAshiHistoricalRequest))
                return false;

            return true;
        }

        private void UpdateLines(int loadedHistoryOffset, int chartOffset)
        {
            var open = LoadedHistory.GetValue(PriceType.Open, loadedHistoryOffset);
            var close = LoadedHistory.GetValue(PriceType.Close, loadedHistoryOffset);
            var high = LoadedHistory.GetValue(PriceType.High, loadedHistoryOffset);
            var low = LoadedHistory.GetValue(PriceType.Low, loadedHistoryOffset);

            double pp, r1, r2, r3, s1, s2, s3;
            pp = r1 = r2 = r3 = s1 = s2 = s3 = 0;

            switch (IndicatorCalculationMethod)
            {
                case CalculationMethod.Classic:
                    pp = (high + low + close) / 3;
                    r1 = 2 * pp - low;
                    r2 = pp + high - low;
                    r3 = 2 * pp + high - 2 * low;
                    s1 = 2 * pp - high;
                    s2 = pp + low - high;
                    s3 = 2 * pp + low - 2 * high;
                    break;
                case CalculationMethod.Camarilla:
                    pp = (high + low + close) / 3;
                    r1 = close + 0.0916 * (high - low);
                    r2 = close + 0.183 * (high - low);
                    r3 = close + 0.275 * (high - low);
                    s1 = close - 0.0916 * (high - low);
                    s2 = close - 0.183 * (high - low);
                    s3 = close - 0.275 * (high - low);
                    break;
                case CalculationMethod.Fibonacci:
                    pp = (high + low + close) / 3;
                    r1 = pp + 0.382 * (high - low);
                    r2 = pp + 0.618 * (high - low);
                    r3 = pp + (high - low);
                    s1 = pp - 0.382 * (high - low);
                    s2 = pp - 0.618 * (high - low);
                    s3 = pp - (high - low);
                    break;
                case CalculationMethod.Woodie:
                    pp = (high + low + 2 * close) / 4;
                    r1 = 2 * pp - low;
                    r2 = pp + high - low;
                    r3 = high + 2 * (pp - low);
                    s1 = 2 * pp - high;
                    s2 = pp + low - high;
                    s3 = low - 2 * (high - pp);
                    break;
                case CalculationMethod.DeMark:
                    {
                        var open0 = loadedHistoryOffset == 0 ? HistoryDataSeries.GetValue(PriceType.Open) : LoadedHistory.GetValue(PriceType.Open, loadedHistoryOffset - 1);

                        var x = 0D;

                        if (close < open0)
                            x = high + 2 * low + close;
                        else if (close > open0)
                            x = 2 * high + low + close;
                        else
                            x = high + low + 2 * close;

                        pp = x / 4;
                        r1 = x / 2 - low;
                        s1 = x / 2 - high;

                    }
                    break;
            }

            Lines["PP"].SetValue(pp, chartOffset);
            Lines["R1"].SetValue(r1, chartOffset);
            Lines["S1"].SetValue(s1, chartOffset);

            if (IndicatorCalculationMethod != CalculationMethod.DeMark)
            {
                Lines["R2"].SetValue(r2, chartOffset);
                Lines["R3"].SetValue(r3, chartOffset);

                Lines["S2"].SetValue(s2, chartOffset);
                Lines["S3"].SetValue(s3, chartOffset);
            }
        }

        private bool IsAllowIndicatorDataRange(Interval loadedInterval)
        {
            TimeSpan loadedHistory = loadedInterval.ToUtc - loadedInterval.FromUtc;
            TimeSpan indicatorBasePeriod = TimeSpan.Zero;
            switch (BasePeriod)
            {
                case Period.Hour:
                    indicatorBasePeriod = TimeSpan.FromHours(PeriodValue);
                    break;
                case Period.Day:
                    indicatorBasePeriod = TimeSpan.FromDays(PeriodValue);
                    break;
                case Period.Week:
                    indicatorBasePeriod = TimeSpan.FromDays(PeriodValue * 7);
                    break;
                case Period.Month:
                    indicatorBasePeriod = TimeSpan.FromDays(PeriodValue * 30);
                    break;
            }
            return loadedHistory >= indicatorBasePeriod;
        }

        void RemovePrevPeriodLines()
        {
            for (int i = 0; i < this.HistoryDataSeries.Count; i++)
            {
                try
                {
                    Lines["PP"].SetEmptyValue(i);
                    Lines["R1"].SetEmptyValue(i);
                    Lines["S1"].SetEmptyValue(i);

                    if (IndicatorCalculationMethod != CalculationMethod.DeMark)
                    {
                        Lines["R1"].SetEmptyValue(i);
                        Lines["R3"].SetEmptyValue(i);

                        Lines["S2"].SetEmptyValue(i);
                        Lines["S3"].SetEmptyValue(i);
                    }
                }
                catch { }
            }
        }

        public enum CalculationMethod
        {
            Classic,
            Camarilla,
            Fibonacci,
            Woodie,
            DeMark
        }
    }
    #endregion
}
