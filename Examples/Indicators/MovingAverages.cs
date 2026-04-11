using Runtime.Script;
using Runtime.Script.Charts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradeApi;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.ToolBelt;


namespace MovingAverages
{
    #region (EMA)Exponential Moving Average
    public class EMA : IndicatorBuilder
    {
        public EMA()
        : base()
        {
            Credentials.ProjectName = "Exponential Moving Average";

            Lines.Set("EMA");
            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of Exponential Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 2;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = string.Format("EMA({0})", MAPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;
            else if (HistoryDataSeries.Count == MAPeriod)
            {
                double sumPrice = 0d;
                for (int i = 0; i < MAPeriod; i++)
                    sumPrice += HistoryDataSeries.GetValue(SourcePrices, i);
                Lines["EMA"].SetValue(sumPrice / MAPeriod);
            }
            else
            {
                double k = 2.0 / (MAPeriod + 1);
                Lines["EMA"].SetValue(k * HistoryDataSeries.GetValue(SourcePrices) + (1 - k) * Lines["EMA"].GetValue(1));
            }
        }
    }
    #endregion

    #region (KAMA)Kaufman Adaptive Moving Average
    public class KAMA : IndicatorBuilder
    {
        public KAMA()
        : base()
        {
            Credentials.ProjectName = "Kaufman Adaptive Moving Average";

            Lines.Set("KAMA");
            Lines["KAMA"].Color = Color.Gray;

            upTrendMarkers = new Dictionary<DateTime, double>();
            downTrendMarkers = new Dictionary<DateTime, double>();
            AMA2 = new List<double>();
            SumAMA = new List<double>();

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int periodAMA = 10;

        [InputParameter(InputType.Numeric, "#Fast", 1)]
        [SimpleNumeric(1)]
        public double nfast = 2.0;

        [InputParameter(InputType.Numeric, "#Slow", 2)]
        [SimpleNumeric(1)]
        public double nslow = 30.0;

        [InputParameter(InputType.Numeric, "G", 3)]
        [SimpleNumeric(1)]
        public double G = 2.0;

        [InputParameter(InputType.Numeric, "dK", 4)]
        [SimpleNumeric(1)]
        public double dK = 2.0;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 5)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter(InputType.Combobox, "KAMA Trend Type", 6)]
        [ComboboxItem("Fixed", AmaTrendType.Fixed)]
        [ComboboxItem("Average", AmaTrendType.Average)]
        public AmaTrendType AMA_Trend_Type;

        [InputParameter(InputType.Color, "Up Trend Marker", 7)]
        public Color upTrendColor = Color.Blue;

        [InputParameter(InputType.Color, "Down Trend Marker", 8)]
        public Color downTrendColor = Color.Red;

        public enum AmaTrendType { Fixed, Average }


        private List<double> AMA2, SumAMA;
        private BuiltInIndicator ma;
        private int winNum;

        Dictionary<DateTime, double> upTrendMarkers, downTrendMarkers;
        SolidBrush upBrush, downBrush;

        public override void Init()
        {
            ScriptShortName = string.Format("KAMA({0};{1};{2};{3};{4})", periodAMA, nfast, nslow, G, dK);

            Lines["KAMA"].SetLineBegin(periodAMA);

            ma = IndicatorsManager.BuildIn.MA(getPrice, periodAMA, MAMode.SMA);
            winNum = ChartSource!= null ? ChartSource.FindWindow(this): 0;

            upTrendMarkers.Clear();
            downTrendMarkers.Clear();
            AMA2.Clear();
            SumAMA.Clear();
        }
        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
            {
                if (AMA_Trend_Type == AmaTrendType.Average)
                {
                    AMA2.Insert(0, 0D);
                    SumAMA.Insert(0, 0D);
                }
                return;
            }

            if (HistoryDataSeries.Count <= periodAMA)
            {
                Lines["KAMA"].SetValue(HistoryDataSeries.GetValue(SourcePrice));
                return;
            }

            double slowSC = (2.0 / (nslow + 1));
            double fastSC = (2.0 / (nfast + 1));
            double dFS = fastSC - slowSC;

            double Noise = ma.GetValue() * periodAMA;
            double ER = (Noise != 0) ? Math.Abs(HistoryDataSeries.GetValue(SourcePrice) - HistoryDataSeries.GetValue(SourcePrice, periodAMA)) / Noise : 0;
            double SSC = Math.Min(Math.Pow(ER * dFS + slowSC, G), 1);

            double prevAma = Lines["KAMA"].GetValue(1);

            double ama = HistoryDataSeries.GetValue(SourcePrice) * SSC + prevAma * (1 - SSC);
            Lines["KAMA"].SetValue(ama);

            double level = 0;
            if (AMA_Trend_Type == AmaTrendType.Fixed)
            {
                level = dK * InstrumentsManager.Current.MinimalTickSize;
            }
            else
            {
                AMA2[0] = ama * ama + AMA2[1];
                SumAMA[0] = ama + SumAMA[1];

                double SredneeAMA = (SumAMA[0] - SumAMA[periodAMA]) / periodAMA;
                double SumKvadratAMA = AMA2[0] - AMA2[periodAMA];
                double dispersion = SumKvadratAMA / periodAMA - SredneeAMA * SredneeAMA;

                double StdAMA = (dispersion >= 0) ? Math.Sqrt(dispersion) : 0;

                level = dK * StdAMA;
            }

            if (Math.Abs(ama - prevAma) > level)
            {
                if (ama - prevAma > 0)
                {
                    upTrendMarkers[HistoryDataSeries.GetTimeUtc()] = ama;
                }
                else
                {
                    downTrendMarkers[HistoryDataSeries.GetTimeUtc()] = ama;
                }
            }
        }

        private double getPrice(int index)
        {
            if ((index + 1) >= HistoryDataSeries.Count)
                return 0;

            return Math.Abs(HistoryDataSeries.GetValue(SourcePrice, index) - HistoryDataSeries.GetValue(SourcePrice, index + 1));
        }

        public override void OnPaintChart(object sender, PaintChartEventArgs args)
        {
            args.Graphics.SetClip(args.Rectangle);

            if (upBrush == null || upBrush.Color != upTrendColor)
                upBrush = new SolidBrush(upTrendColor);

            if (downBrush == null || downBrush.Color != downTrendColor)
                downBrush = new SolidBrush(downTrendColor);

            var start = ChartSource.GetTimeValue(new PointF(0, 0), winNum);
            var end = ChartSource.GetTimeValue(new PointF(args.Rectangle.Width, 0), winNum);

            var width = (HistoryDataSeries.Count > 1) ?
                ChartSource.GetChartPoint(new TimeValue(HistoryDataSeries.GetTimeUtc(), 0), winNum).X - ChartSource.GetChartPoint(new TimeValue(HistoryDataSeries.GetTimeUtc(1), 0), winNum).X
                : ChartSource.BarsWidth;

            foreach (var key in upTrendMarkers.Keys)
            {
                if (key >= start.TimeUtc && key <= end.TimeUtc)
                {
                    var price = upTrendMarkers[key];
                    var loc = ChartSource.GetChartPoint(new TimeValue(key, price), winNum);
                    args.Graphics.FillEllipse(upBrush, loc.X + width / 4, loc.Y + width, width / 2, width / 2);
                }
            }

            foreach (var key in downTrendMarkers.Keys)
            {
                if (key >= start.TimeUtc && key <= end.TimeUtc)
                {
                    var price = downTrendMarkers[key];
                    var loc = ChartSource.GetChartPoint(new TimeValue(key, price), winNum);
                    args.Graphics.FillEllipse(downBrush, loc.X + width / 4, loc.Y - width, width / 2, width / 2);
                }
            }
        }

        public override void Complete()
        {
            upTrendMarkers.Clear();
            downTrendMarkers.Clear();
            AMA2.Clear();
            SumAMA.Clear();
        }
    }
    #endregion

    #region (LWMA)Linear Weighted Moving Average
    public class LWMA : IndicatorBuilder
    {
        public LWMA()
            : base()
        {
            Credentials.ProjectName = "Linear Weighted Moving Average";

            Lines.Set("LWMA");
        }

        [InputParameter(InputType.Numeric, "Period of Linear Weighted Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 2;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = string.Format("LWMA({0})", MAPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count <= MAPeriod)
                return;

            double numerator = 0.0;
            double denominator = 0.0;
            for (int i = 0; i < MAPeriod; i++)
            {
                int weight = (MAPeriod - i);
                numerator += weight * HistoryDataSeries.GetValue(SourcePrice, i);
                denominator += weight;
            }
            Lines["LWMA"].SetValue(numerator / denominator);
        }
    }
    #endregion

    #region (MAS3)3MASignal
    public class MAS3 : IndicatorBuilder
    {
        public MAS3()
            : base()
        {
            Credentials.ProjectName = "3MASignal";

            Lines.Set("3MA");
            Lines["3MA"].Style = LineStyle.Histogram;
            Lines["3MA"].Width = 5;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Short Moving Average Period", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int ShortMaPeriod = 5;

        [InputParameter(InputType.Numeric, "Middle Moving Average Period", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int MiddleMaPeriod = 10;

        [InputParameter(InputType.Numeric, "Long Moving Average Period", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int LongMaPeriod = 25;

        [InputParameter(InputType.Numeric, "Amount of bars passed before opening position", 3)]
        [SimpleNumeric(1D, 99999D)]
        public int BarsInterval = 1;

        public enum MAS3TrendType
        {
            DOWN_TREND = -1,
            NO_TREND = 0,
            UP_TREND = 1,
        }

        private BuiltInIndicator shortMa, middleMa, longMa;

        public override void Init()
        {
            ScriptShortName = string.Format("MAS3({0};{1};{2})", ShortMaPeriod, MiddleMaPeriod, LongMaPeriod);

            shortMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, ShortMaPeriod, MAMode.SMA);
            middleMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MiddleMaPeriod, MAMode.SMA);
            longMa = IndicatorsManager.BuildIn.MA(HistoryDataSeries, LongMaPeriod, MAMode.SMA);
        }

        public override void Update(TickStatus args)
        {
            Lines["3MA"].SetValue((int)TradeSignal());
        }

        public int GetMaxPeriod(int shortPeriod, int middlePeriod, int longPeriod)
        {
            int maxValue;
            if (shortPeriod > middlePeriod)
                maxValue = shortPeriod;
            else
                maxValue = longPeriod;

            if (maxValue > longPeriod)
                return maxValue;
            else
                return longPeriod;
        }

        public MAS3TrendType CompareMA(double shMa, double midMA, double lgMa)
        {
            if ((midMA > lgMa) && (midMA < shMa))
            {
                return MAS3TrendType.UP_TREND;
            }
            if ((midMA > shMa) && (midMA < lgMa))
            {
                return MAS3TrendType.DOWN_TREND;
            }
            return MAS3TrendType.NO_TREND;
        }

        public MAS3TrendType TradeSignal()
        {
            MAS3TrendType state = MAS3TrendType.NO_TREND;

            if (HistoryDataSeries.Count < GetMaxPeriod(ShortMaPeriod, MiddleMaPeriod, LongMaPeriod))
                return state;

            for (int shift = 0; shift < BarsInterval; shift++)
            {
                if (shift == 0)
                {
                    state = CompareMA(shortMa.Lines[0][shift], middleMa.Lines[0][shift], longMa.Lines[0][shift]);
                }
                else
                {
                    MAS3TrendType curState = CompareMA(shortMa.Lines[0][shift], middleMa.Lines[0][shift], longMa.Lines[0][shift]);
                    if (state == MAS3TrendType.UP_TREND)
                    {
                        if (curState != MAS3TrendType.UP_TREND)
                            return MAS3TrendType.NO_TREND;
                    }
                    if (state == MAS3TrendType.DOWN_TREND)
                    {
                        if (curState != MAS3TrendType.DOWN_TREND)
                            return MAS3TrendType.NO_TREND;
                    }
                }
            }
            return state;
        }
    }
    #endregion

    #region (MMA)Modified Moving Average
    public class MMA : IndicatorBuilder
    {
        public MMA()
            : base()
        {
            Credentials.ProjectName = "Modified Moving Average";

            Lines.Set("MMA");
            Lines["MMA"].Color = Color.Lime;
            Lines["MMA"].Width = 4;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of Modified Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 2;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = string.Format("MMA({0})", MAPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;
            else if (HistoryDataSeries.Count == MAPeriod)
            {
                double sumPrice = 0d;
                for (int i = 0; i < MAPeriod; i++)
                    sumPrice += HistoryDataSeries.GetValue(SourcePrice, i);
                Lines["MMA"].SetValue(sumPrice / MAPeriod);
            }
            else
                Lines["MMA"].SetValue((HistoryDataSeries.GetValue(SourcePrice) + (MAPeriod - 1) * Lines["MMA"].GetValue(1)) / MAPeriod);
        }
    }
    #endregion

    #region (PPMA)Pivot Point Moving Average
    public class PPMA : IndicatorBuilder
    {
        public PPMA()
            : base()
        {
            Credentials.ProjectName = "Pivot Point Moving Average";

            Lines.Set("PPMA");

            Lines.Set("PPMA_MA");
            Lines["PPMA_MA"].Color = Color.Yellow;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of PPMA", 0)]
        [SimpleNumeric(1D, 100D)]
        public int PPMAPeriod = 5;

        public override void Init()
        {
            ScriptShortName = string.Format("PPMA({0})", PPMAPeriod);

            Lines["PPMA"].SetLineBegin(PPMAPeriod);
            Lines["PPMA_MA"].SetLineBegin(PPMAPeriod);
        }

        public override void Update(TickStatus args)
        {
            double pivot = (HistoryDataSeries.GetValue(PriceType.High)
                + HistoryDataSeries.GetValue(PriceType.Low)
                + HistoryDataSeries.GetValue(PriceType.Close)) / 3.0;

            Lines["PPMA"].SetValue(pivot);

            if (HistoryDataSeries.Count < PPMAPeriod)
                return;

            double sum = pivot;
            for (int j = 1; j < PPMAPeriod; j++)
                sum += (HistoryDataSeries.GetValue(PriceType.High, j)
                    + HistoryDataSeries.GetValue(PriceType.Low, j)
                    + HistoryDataSeries.GetValue(PriceType.Close, j)) / 3.0;

            Lines["PPMA_MA"].SetValue(sum / PPMAPeriod);
        }
    }
    #endregion

    #region (SMA)Simple Moving Average
    public class SMA : IndicatorBuilder
    {
        public SMA()
            : base()
        {
            Credentials.ProjectName = "Simple Moving Average";
            Lines.Set("SMA");
            Lines["SMA"].Color = Color.Red;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of Simple Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 1;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        private BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = string.Format("SMA({0})", MAPeriod);
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAMode.SMA, 0, SourcePrices);
        }

        public override void Update(TickStatus args)
        {
            Lines["SMA"].SetValue(ma.GetValue());
        }
    }
    #endregion

    #region (SMMA)Smoothed Moving Average
    public class SMMA : IndicatorBuilder
    {
        public SMMA()
            : base()
        {
            Credentials.ProjectName = "Smoothed Moving Average";

            Lines.Set("SMMA");
            Lines["SMMA"].Color = Color.Red;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of Smoothed Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 2;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = string.Format("SMMA({0})", MAPeriod);
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;

            if (count < MAPeriod)
                return;

            if (count == MAPeriod)
            {
                double sum = 0.0;
                for (int i = 0; i < MAPeriod; i++)
                    sum += HistoryDataSeries.GetValue(SourcePrice, i);

                Lines["SMMA"].SetValue(sum / MAPeriod);
            }
            else
            {
                double prevMa = Lines["SMMA"].GetValue(1);
                Lines["SMMA"].SetValue((prevMa * (MAPeriod - 1) + HistoryDataSeries.GetValue(SourcePrice)) / MAPeriod);
            }
        }
    }
    #endregion

    #region (MD)McGinley Dynamic
    public class MD : IndicatorBuilder
    {
        public MD()
            : base()
        {
            Credentials.ProjectName = "McGinley Dynamic";

            Lines.Set("MD");
            Lines["MD"].Color = Color.Red;
            Lines["MD"].Width = 2;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Number Of Bars (0=All)", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int NumberOfBars = 1000;

        [InputParameter(InputType.Numeric, "Periods", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int Periods = 12;

        [InputParameter(InputType.Numeric, "Smoothing", 3)]
        [SimpleNumeric(1D, 99999D)]
        public int Smoothing = 125;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        private BuiltInIndicator ema;

        public override void Init()
        {
            ScriptShortName = string.Format("MD({0})", Periods);

            Lines["MD"].SetLineBegin(Periods);
            ema = IndicatorsManager.BuildIn.MA(HistoryDataSeries, Periods, MAMode.EMA, 0, SourcePrices);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < 2)
                return;

            double value = ema.Lines[0].GetValue(1);
            double price = HistoryDataSeries.GetValue(SourcePrices);
            Lines["MD"].SetValue(value + (price - value) / (price / value * Smoothing));
        }
    }
    #endregion

    #region (REGRESSION)Regression Line
    public class REGRESSION : IndicatorBuilder
    {
        public REGRESSION()
            : base()
        {
            Credentials.ProjectName = "Regression Line";

            Lines.Set("REGRESSION");
            Lines["REGRESSION"].Color = Color.Crimson;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of Linear Regression", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int LRIPeriod = 2;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = string.Format("REGRESSION({0})", LRIPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < LRIPeriod)
                return;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXY = 0.0;
            double sumXX = 0.0;

            for (int i = 0; i < LRIPeriod; i++)
            {
                double price = HistoryDataSeries.GetValue(SourcePrices, i);
                sumX += i;
                sumY += price;
                sumXY += i * price;
                sumXX += price * price;
            }

            double p = (LRIPeriod * sumXY - sumX * sumY) / (LRIPeriod * sumXX - sumX * sumX);
            double b = (sumY * sumXX - sumX * sumXY) / (LRIPeriod * sumXX - sumX * sumX);

            Lines["REGRESSION"].SetValue(p * LRIPeriod + b);
        }
    }
    #endregion
}
