﻿﻿﻿﻿using Runtime.Script;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradeApi;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.ToolBelt;

namespace Oscillators
{
    #region (AC)Accelerator
    public class AC : IndicatorBuilder
    {
        public AC()
            : base()
        {
            Credentials.ProjectName = "Acceleration Oscillator";

            ScriptShortName = "AC";

            Lines.Set("asc");
            Lines["asc"].Color = Color.Green;
            Lines["asc"].Style = LineStyle.Histogram;

            Lines.Set("desc");
            Lines["desc"].Color = Color.Red;
            Lines["desc"].Style = LineStyle.Histogram;

            base.SeparateWindow = true;
        }

        private BuiltInIndicator ac;


        public override void Init()
        {
            ac = IndicatorsManager.BuildIn.AC(HistoryDataSeries);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < 2)
                return;

            double curr = ac.GetValue(0);
            double prev = ac.GetValue(1);

            if (prev < curr)
            {
                Lines["asc"].SetValue(curr);
                Lines["desc"].SetValue(0);
            }
            else if (prev > curr)
            {
                Lines["asc"].SetValue(0);
                Lines["desc"].SetValue(curr);
            }
            else
            {
                Lines["asc"].SetValue(0);
                Lines["desc"].SetValue(0);
            }
        }
    }
    #endregion

    #region (AO)Awesome
    public class AO : IndicatorBuilder
    {
        public AO()
            : base()
        {
            Credentials.ProjectName = "Awesome Oscillator";

            Lines.Set("asc");
            Lines["asc"].Color = Color.Green;
            Lines["asc"].Style = LineStyle.Histogram;

            Lines.Set("desc");
            Lines["desc"].Color = Color.Red;
            Lines["desc"].Style = LineStyle.Histogram;

            SeparateWindow = true;
        }

        BuiltInIndicator ao;

        public override void Init()
        {
            ao = IndicatorsManager.BuildIn.AO(HistoryDataSeries);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < 2)
                return;

            double curr = ao.GetValue(0);
            double prev = ao.GetValue(1);

            if (prev < curr)
            {
                Lines["asc"].SetValue(curr);
                Lines["desc"].SetValue(0);
            }
            else if (prev > curr)
            {
                Lines["asc"].SetValue(0);
                Lines["desc"].SetValue(curr);
            }
            else
            {
                Lines["asc"].SetValue(0);
                Lines["desc"].SetValue(0);
            }
        }
    }
    #endregion

    #region (AROON)Aroon

    public class AROON : IndicatorBuilder
    {
        public AROON()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "Aroon";
            #endregion

            Lines.Set("asc");
            Lines["asc"].Color = Color.Lime;

            Lines.Set("desc");
            Lines["desc"].Color = Color.Teal;
            Lines["desc"].Style = LineStyle.DotSpaced;


            Thresholds.Set("up");
            Thresholds["up"].Color = Color.Red;
            Thresholds["up"].Level = 70;

            Thresholds.Set("down");
            Thresholds["down"].Color = Color.Red;
            Thresholds["down"].Level = 30;
            Thresholds["down"].Style = LineStyle.DotSpaced;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period of loking back", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int IndPeriod = 14;

        public override void Init()
        {
            ScriptShortName = $"AROON({IndPeriod})";
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < IndPeriod)
                return;

            double high = HistoryDataSeries.GetValue(PriceType.High);
            double low = HistoryDataSeries.GetValue(PriceType.Low);
            double perHigh = 0;
            double perLow = 0;
            for (int i = 1; i < IndPeriod; i++)
            {
                double price = HistoryDataSeries.GetValue(PriceType.High, i);
                if (price > high)
                {
                    high = price;
                    perHigh = i;
                }

                price = HistoryDataSeries.GetValue(PriceType.Low, i);
                if (price < low)
                {
                    low = price;
                    perLow = i;
                }
            }

            Lines["asc"].SetValue((1.0 - perHigh / IndPeriod) * 100);
            Lines["desc"].SetValue((1.0 - perLow / IndPeriod) * 100);
        }
    }
    #endregion

    #region (CCI)Commodity Channel Index
    public class CCI : IndicatorBuilder
    {
        public CCI()
            : base()
        {
            Credentials.ProjectName = "Commodity Channel Index";

            Lines.Set("cci");
            Lines["cci"].Color = Color.Red;

            Thresholds.Set("150");
            Thresholds["150"].Color = Color.Gray;
            Thresholds["150"].Level = 150;

            Thresholds.Set("100");
            Thresholds["100"].Color = Color.Gray;
            Thresholds["100"].Level = 100;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            Thresholds.Set("-100");
            Thresholds["-100"].Color = Color.Gray;
            Thresholds["-100"].Level = -100;

            Thresholds.Set("-150");
            Thresholds["-150"].Color = Color.Gray;
            Thresholds["-150"].Level = -150;

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

        BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = $"CCI({MAPeriod})";
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAType, 0, PriceType.Typical);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;

            double val = ma.GetValue();

            double d = 0;
            for (int i = 0; i < MAPeriod; i++)
                d += Math.Abs(HistoryDataSeries.GetValue(PriceType.Typical, i) - val);

            d = 0.015 * (d / MAPeriod);
            Lines["cci"].SetValue((HistoryDataSeries.GetValue(PriceType.Typical) - val) / d);
        }
    }
    #endregion

    #region (CMO)Chande Momentum Oscillator
    public class CMO : IndicatorBuilder
    {
        public CMO()
            : base()
        {
            Credentials.ProjectName = "Chande Momentum Oscillator";

            Lines.Set("CMO");
            Lines["CMO"].Color = Color.Purple;

            Thresholds.Set("Up");
            Thresholds["Up"].Level = 50;

            Thresholds.Set("Down");
            Thresholds["Down"].Level = -50;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period of MA for envelopes", 0)]
        [SimpleNumeric(1D, 1000D)]
        public int MAPeriod = 8;

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
            ScriptShortName = $"CMO({MAPeriod})";
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count <= MAPeriod)
                return;

            double sum1 = 0.0;
            double sum2 = 0.0;
            for (int i = 0; i < MAPeriod; i++)
            {
                double diff = HistoryDataSeries.GetValue(SourcePrice, i) - HistoryDataSeries.GetValue(SourcePrice, i + 1);
                if (diff > 0)
                    sum1 += diff;
                else
                    sum2 -= diff;
            }

            // Setting value
            Lines["CMO"].SetValue(100.0 * ((sum1 - sum2) / (sum1 + sum2)));
        }
    }
    #endregion

    #region (KRI)Kairi Relative Index
    public class KRI : IndicatorBuilder
    {
        public KRI()
            : base()
        {
            Credentials.ProjectName = "Kairi Relative Index";

            Lines.Set("KRI");
            Lines["KRI"].Color = Color.Red;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "KRIPeriod", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int KRIPeriod = 13;

        BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = $"KRI({KRIPeriod})";
            Lines[0].SetLineBegin(KRIPeriod);
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, KRIPeriod, MAMode.SMA);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count <= KRIPeriod)
                return;

            double val = ma.GetValue();
            Lines["KRI"].SetValue((HistoryDataSeries.GetValue(PriceType.Close) - val) / val * 100);
        }
    }
    #endregion

    #region (MACD)Moving Average/Convergence Divergence
    public class MACD : IndicatorBuilder
    {
        public MACD()
            : base()
        {
            Credentials.ProjectName = "Moving Average/Convergence Divergence";

            Lines.Set("MACD");
            Lines["MACD"].Color = Color.DodgerBlue;

            Lines.Set("Signal");
            Lines["Signal"].Color = Color.Red;

            Lines.Set("Histogram");
            Lines["Histogram"].Color = Color.Green;
            Lines["Histogram"].Width = 3;
            Lines["Histogram"].Style = LineStyle.Histogram;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "FastEMA", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int FastEMA = 12;

        [InputParameter(InputType.Numeric, "SlowEMA", 1)]
        [SimpleNumeric(1.0, 99999D)]
        public int SlowEMA = 26;

        [InputParameter(InputType.Numeric, "SignalSMA", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int SignalSMA = 9;

        BuiltInIndicator fastEMA;
        BuiltInIndicator slowEMA;

        public override void Init()
        {
            ScriptShortName = $"MACD({FastEMA},{SlowEMA},{SignalSMA})";

            fastEMA = IndicatorsManager.BuildIn.MA(HistoryDataSeries, FastEMA, MAMode.EMA);
            slowEMA = IndicatorsManager.BuildIn.MA(HistoryDataSeries, SlowEMA, MAMode.EMA);
        }

        private double SMA()
        {
            int count = HistoryDataSeries.Count;
            if (count < FastEMA + SignalSMA
                || count < SlowEMA + SignalSMA)
                return double.NaN;

            double summa = 0.0;
            for (int i = 0; i < SignalSMA; i++)
                summa += Lines["MACD"].GetValue(i) / SignalSMA;

            return summa;
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;
            if (count < FastEMA || count < SlowEMA)
                return;

            double differ = fastEMA.GetValue() - slowEMA.GetValue();
            Lines["MACD"].SetValue(differ);

            double signal = SMA();
            if (!double.IsNaN(signal))
                Lines["Signal"].SetValue(signal);

            Lines["Histogram"].SetValue(differ - signal);
        }
    }
    #endregion

    #region (MOMENTUM)Momentum
    public class MOMENTUM : IndicatorBuilder
    {
        public MOMENTUM()
            : base()
        {
            Credentials.ProjectName = "Momentum";

            Lines.Set("MOMENTUM");
            Lines["MOMENTUM"].Color = Color.Red;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period of momentum:", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MomPeriod = 20;

        public override void Init()
        {
            ScriptShortName = $"MOMENTUM({MomPeriod})";
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;
            if (count <= MomPeriod)
                return;

            Lines["MOMENTUM"].SetValue(HistoryDataSeries.GetValue(PriceType.Close) -
                 (HistoryDataSeries.GetValue(PriceType.Close, MomPeriod)));
        }
    }
    #endregion

    #region (PO)Price Oscillator
    public class PO : IndicatorBuilder
    {
        public PO()
            : base()
        {
            Credentials.ProjectName = "Price Oscillator";

            Lines.Set("PO");

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Type of Moving Average", 0)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter(InputType.Numeric, "Period of MA1", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod1 = 2;

        [InputParameter(InputType.Numeric, "Period of MA2", 3)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod2 = 25;

        BuiltInIndicator slow_ma;
        BuiltInIndicator fast_ma;

        public override void Init()
        {
            ScriptShortName = $"PO({MAPeriod1},{MAPeriod2})";
            fast_ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod1, MAType, 0, SourcePrice); ;
            slow_ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod2, MAType, 0, SourcePrice);
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;
            if (count < MAPeriod1 || count < MAPeriod2)
                return;

            Lines["PO"].SetValue(fast_ma.GetValue() - slow_ma.GetValue());
        }
    }
    #endregion

    #region (PPO)Percentage Price Oscillator
    public class PPO : IndicatorBuilder
    {
        public PPO()
            : base()
        {
            Credentials.ProjectName = "Percentage Price Oscillator";

            Lines.Set("PPO");
            Lines["PPO"].Color = Color.SkyBlue;
            Lines["PPO"].Width = 2;

            Lines.Set("Signal");
            Lines["Signal"].Color = Color.Red;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Fast EMA Period", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int fastEmaPeriod = 12;

        [InputParameter(InputType.Numeric, "Slow EMA Period", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int slowEmaPeriod = 26;

        [InputParameter(InputType.Numeric, "Signal EMA Period", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int signalEmaPeriod = 9;

        private BuiltInIndicator FastEMA;
        private BuiltInIndicator SlowEMA;

        public override void Init()
        {
            ScriptShortName = $"PPO({fastEmaPeriod},{slowEmaPeriod},{signalEmaPeriod})";

            FastEMA = IndicatorsManager.BuildIn.MA(HistoryDataSeries, fastEmaPeriod, MAMode.EMA);
            SlowEMA = IndicatorsManager.BuildIn.MA(HistoryDataSeries, slowEmaPeriod, MAMode.EMA);
        }

        public override void Update(TickStatus args)
        {
            GetPPO();
            GetSignalPPO();
        }

        private void GetPPO()
        {
            int maxPPO = Math.Max(fastEmaPeriod, slowEmaPeriod);

            if (HistoryDataSeries.Count < maxPPO)
                return;

            double fastEMA = FastEMA.GetValue();
            double slowEMA = SlowEMA.GetValue();

            var ppo = slowEMA != 0 ? ((fastEMA - slowEMA) / slowEMA) : 0D;

            //ppo *= 100;

            Lines["PPO"].SetValue(ppo);
        }

        private void GetSignalPPO()
        {
            int maxPPO = Math.Max(fastEmaPeriod, slowEmaPeriod);
            int maxSignalPPO = maxPPO + signalEmaPeriod - 1;
            var count = HistoryDataSeries.Count;

            if (count < maxSignalPPO)
                return;

            else if (count == maxSignalPPO)
            {
                double sumPrice = 0d;
                for (int i = 0; i < signalEmaPeriod; i++)
                    sumPrice += Lines["PPO"].GetValue(i);

                Lines["Signal"].SetValue(sumPrice / signalEmaPeriod);
            }
            else
            {
                double prevEMA = Lines["Signal"].GetValue(1);
                double k = 2.0 / (signalEmaPeriod + 1);
                var price = Lines["PPO"].GetValue();
                Lines["Signal"].SetValue(k * price + (1 - k) * prevEMA);
            }
        }
    }
    #endregion

    #region (RLW)%R Larry Williams
    public class RLW : IndicatorBuilder
    {
        public RLW()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "%R Larry Williams";
            #endregion

            Lines.Set("RLW");
            Lines["RLW"].Color = Color.SkyBlue;
            Lines["RLW"].Width = 2;

            Thresholds.Set("UpperBound");
            Thresholds["UpperBound"].Color = Color.Red;
            Thresholds["UpperBound"].Width = 2;
            Thresholds["UpperBound"].Level = -20;

            Thresholds.Set("LowerBound");
            Thresholds["LowerBound"].Color = Color.Red;
            Thresholds["LowerBound"].Width = 2;
            Thresholds["LowerBound"].Level = -80;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "ExtWPRPeriod", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int ExtWPRPeriod = 14;

        public override void Init()
        {
            ScriptShortName = $"RLW({ExtWPRPeriod})";
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < ExtWPRPeriod)
                return;
            Interval interval = new Interval(HistoryDataSeries.GetTimeUtc(ExtWPRPeriod - 1), HistoryDataSeries.GetTimeUtc());
            double highest = HistoryDataSeries.GetValue(PriceType.High, HistoryDataSeries.GetHighest(PriceType.High, interval)[0]);
            double lowest = HistoryDataSeries.GetValue(PriceType.Low, HistoryDataSeries.GetLowest(PriceType.Low, interval)[0]);
            if ((highest - lowest) > 0.0000001)
                Lines["RLW"].SetValue(-100 * (highest - HistoryDataSeries.GetValue(PriceType.Close)) / (highest - lowest));
        }
    }
    #endregion

    #region (ROC)Rate of Change
    public class ROC : IndicatorBuilder
    {
        public ROC()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "Rate of Change";
            #endregion

            Lines.Set("ROC");
            Lines["ROC"].Color = Color.Brown;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "ExtWPRPeriod", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int RocPeriod = 20;

        public override void Init()
        {
            ScriptShortName = $"ROC({RocPeriod})";
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count <= RocPeriod)
                return;

            double price = HistoryDataSeries.GetValue(PriceType.Close);
            double pricen = HistoryDataSeries.GetValue(PriceType.Close, RocPeriod);
            Lines["ROC"].SetValue(100 * (price - pricen) / pricen);
        }
    }
    #endregion

    #region (QSTICK)QStick
    public class QSTICK : IndicatorBuilder
    {
        public QSTICK()
            : base()
        {
            Credentials.ProjectName = "QStick";

            Lines.Set("QSTICK");

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Type of Moving Average", 0)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period", 1)]
        [SimpleNumeric(1.0, 99999.0)]
        public int MAPeriod = 20;

        BuiltInIndicator signal;

        public override void Init()
        {
            ScriptShortName = $"QSTICK({MAPeriod})";
            signal = IndicatorsManager.BuildIn.MA(getSourceForIndicator, MAPeriod, MAType);
        }

        private double getSourceForIndicator(int index = 0)
        {
            return HistoryDataSeries.GetValue(PriceType.Close, index) -
                HistoryDataSeries.GetValue(PriceType.Open, index);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count > MAPeriod)
                Lines["QSTICK"].SetValue(signal.GetValue());
            else if (MAType == MAMode.EMA)
                Lines["QSTICK"].SetValue(getSourceForIndicator());
        }
    }
    #endregion

    #region (RSI)Relative Strength Index
    public class RSI : IndicatorBuilder
    {
        public RSI() :
            base()
        {
            Credentials.ProjectName = "Relative Strength Index";

            Lines.Set("RSI");
            Lines["RSI"].Color = Color.Green;

            Lines.Set("RSI MA");
            Lines["RSI MA"].Color = Color.SkyBlue;

            Thresholds.Set("Upper Bound");
            Thresholds["Upper Bound"].Color = Color.Red;
            Thresholds["Upper Bound"].Level = 70;

            Thresholds.Set("Lower Bound");
            Thresholds["Lower Bound"].Color = Color.FromArgb(0, 51, 252);
            Thresholds["Lower Bound"].Level = 30;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "RSI Period", 0)]
        [SimpleNumeric(0D, 100D)]
        public int RSIPeriod = 20;

        [InputParameter(InputType.Combobox, "RSI Method", 1)]
        [ComboboxItem("Simple", RSIMode.Simple)]
        [ComboboxItem("Exponential", RSIMode.Exponential)]
        public RSIMode RSIMethod = RSIMode.Exponential;

        [InputParameter(InputType.Numeric, "Period of Moving Average", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 5;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 3)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        private double prevV = 0.0;
        private double prevP = 0.0;
        private double sumV = 0.0;
        private double sumP = 0.0;

        public override void Init()
        {
            ScriptShortName = $"RSI({RSIPeriod},{MAPeriod})";

            Lines[0].SetLineBegin(RSIPeriod);
            Lines[1].SetLineBegin(RSIPeriod + MAPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
            {
                prevV = sumV;
                prevP = sumP;
                return;
            }

            if (HistoryDataSeries.Count <= RSIPeriod || HistoryDataSeries.Count <= MAPeriod)
                return;

            if (RSIMethod == RSIMode.Simple)
                CalcSimple();
            else
                CalcExpon();

            Lines["RSI MA"].SetValue(GetMA());
        }

        public void CalcSimple()
        {
            sumV = 0.0;
            sumP = 0.0;
            for (int i = 0; i < RSIPeriod; i++)
            {
                double diff = HistoryDataSeries.GetValue(PriceType.Close, i) - HistoryDataSeries.GetValue(PriceType.Close, i + 1);
                if (diff > 0.0)
                    sumV += diff;
                else
                    sumP -= diff;
            }

            double rsi = (sumP != 0) ? 100.0 * (1.0 - 1.0 / (1.0 + sumV / sumP)) : 100D;
            Lines["RSI"].SetValue(rsi);
        }

        public void CalcExpon()
        {
            if (HistoryDataSeries.Count == RSIPeriod + 1)
            {
                CalcSimple();
                prevV = sumV = (sumV / RSIPeriod);
                prevP = sumP = (sumP / RSIPeriod);
            }
            else
            {
                double diff = HistoryDataSeries.GetValue(PriceType.Close) - HistoryDataSeries.GetValue(PriceType.Close, 1);
                if (diff > 0)
                {
                    sumV = (prevV * (RSIPeriod - 1) + diff) / RSIPeriod;
                    sumP = (prevP * (RSIPeriod - 1)) / RSIPeriod;
                }
                else
                {
                    sumV = (prevV * (RSIPeriod - 1)) / RSIPeriod;
                    sumP = (prevP * (RSIPeriod - 1) - diff) / RSIPeriod;
                }
            }

            double rsi = (sumP != 0) ? 100D - 100D / (1 + sumV / sumP) : 0D;
            Lines["RSI"].SetValue(rsi);
        }

        public double GetSMA()
        {
            double summa = 0.0;
            for (int i = 0; i < MAPeriod; i++)
            {
                double val = Lines["RSI"].GetValue(i);
                summa += val != int.MaxValue ? val : 0;
            }
            return summa / MAPeriod;
        }

        public double GetEMA()
        {
            double k = 2.0 / (MAPeriod + 1);

            double summa = Lines["RSI"].GetValue(MAPeriod - 1);
            for (int i = MAPeriod - 2; i >= 0; i--)
            {
                double price = Lines["RSI"].GetValue(i);
                summa += k * (price - summa);
            }
            return summa;
        }

        public double GetMMA()
        {
            double mma = Lines["RSI"].GetValue(0);
            double k = 1.0 / MAPeriod;
            for (int i = 0; i < MAPeriod; i++)
            {
                double price = Lines["RSI"].GetValue(i);
                mma = price * k + mma * (1.0 - k);
            }
            return mma;
        }

        public double GetLWMA()
        {
            double numerator = 0.0;
            double denominator = 0.0;
            double k = 1.0 / MAPeriod;
            int period = MAPeriod;

            for (int i = 0; i < MAPeriod; i++)
            {
                double val = Lines["RSI"].GetValue(i);
                numerator += period * (val != int.MaxValue ? val : 0);
                denominator += period;
                period--;
            }
            if (denominator > 0.0)
                return numerator / denominator;
            else
                return 0.0;
        }

        public double GetMA()
        {
            double result = 0.0;
            switch (MAType)
            {
                case MAMode.SMA:
                    result = GetSMA();
                    break;
                case MAMode.EMA:
                    result = GetEMA();
                    break;
                case MAMode.LWMA:
                    result = GetLWMA();
                    break;
                case MAMode.SMMA:
                    result = GetMMA();
                    break;
            }
            return result;
        }
    }

    #endregion

    #region (STOCHASTIC)Stochastic
    public class STOCHASTIC : IndicatorBuilder
    {
        public STOCHASTIC()
            : base()
        {
            Credentials.ProjectName = "Stochastic Slow";

            Lines.Set("STOCHASTIC");
            Lines["STOCHASTIC"].Color = Color.Green;

            Lines.Set("STOCHASTIC MA");
            Lines["STOCHASTIC MA"].Color = Color.LightSkyBlue;

            Thresholds.Set("Upper Bound");
            Thresholds["Upper Bound"].Color = Color.Red;
            Thresholds["Upper Bound"].Level = 80;
            Thresholds["Upper Bound"].Style = LineStyle.DotSpaced;

            Thresholds.Set("Lower Bound");
            Thresholds["Lower Bound"].Color = Color.FromArgb(0, 51, 252);
            Thresholds["Lower Bound"].Level = 20;
            Thresholds["Lower Bound"].Style = LineStyle.DotSpaced;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period of indicator", 1)]
        [SimpleNumeric(1D, 999D)]
        public int IndPeriod = 10;


        [InputParameter(InputType.Numeric, "Smoothing", 2)]
        [SimpleNumeric(1D, 999D)]
        public int Smooth = 3;

        [InputParameter(InputType.Numeric, "Double smoothing", 2)]
        [SimpleNumeric(1D, 999D)]
        public int DoubleSmooth = 3;

        CustomData customData;

        public override void Init()
        {
            Credentials.ProjectName = $"SSD({IndPeriod})";
            customData = new CustomData(HistoryDataSeries);
        }

        private double getSSD()
        {
            double high = HistoryDataSeries.GetValue(PriceType.High);
            double low = HistoryDataSeries.GetValue(PriceType.Low);
            double close = HistoryDataSeries.GetValue(PriceType.Close);

            for (int i = 0; i < IndPeriod; i++)
            {
                double price = HistoryDataSeries.GetValue(PriceType.High, i);
                if (price > high)
                    high = price;
                price = HistoryDataSeries.GetValue(PriceType.Low, i);
                if (price < low)
                    low = price;
            }

            high -= low;
            return high != 0.0 ? 100 * (close - low) / high : 0.0;
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;
            if (count < IndPeriod)
                return;

            var stoch = getSSD();
            customData.SetValue(PriceType.Close, stoch);

            if (count < IndPeriod + Smooth - 1)
                return;

            double sum = 0;
            for (int k = 0; k < Smooth; k++)
                sum += customData.GetValue(PriceType.Close, k);

            Lines["STOCHASTIC"].SetValue(sum / Smooth);

            if (count < IndPeriod + Smooth + DoubleSmooth - 2)
                return;

            sum = 0;
            for (int k = 0; k < DoubleSmooth; k++)
                sum += Lines["STOCHASTIC"].GetValue(k);

            Lines["STOCHASTIC MA"].SetValue(sum / DoubleSmooth);
        }

        public override void Complete()
        {
            
        }
    }
    #endregion

    #region (TSI)True Strength Index
    public class TSI : IndicatorBuilder
    {
        public TSI()
            : base()
        {

            Credentials.ProjectName = "True Strength Index";

            Lines.Set("TSI");
            Lines["TSI"].Color = Color.FromArgb(0, 51, 252);

            Thresholds.Set("Upper Bound");
            Thresholds["Upper Bound"].Color = Color.Gray;
            Thresholds["Upper Bound"].Level = 50;

            Thresholds.Set("Zero");
            Thresholds["Zero"].Color = Color.Gray;
            Thresholds["Zero"].Level = 0;

            Thresholds.Set("Lower Bound");
            Thresholds["Lower Bound"].Color = Color.Gray;
            Thresholds["Lower Bound"].Level = -50;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "First R", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int First_R = 5;

        [InputParameter(InputType.Numeric, "Second S", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int Second_S = 8;

        private BuiltInIndicator ema_mtm;
        private BuiltInIndicator ema_absmtm;
        private BuiltInIndicator ema2_mtm;
        private BuiltInIndicator ema2_absmtm;

        public override void Init()
        {
            ScriptShortName = $"TSI({First_R},{Second_S})";

            ema_mtm = IndicatorsManager.BuildIn.MA(getMTM, First_R, MAMode.EMA);
            ema_absmtm = IndicatorsManager.BuildIn.MA(getAbsMTM, First_R, MAMode.EMA);
            ema2_mtm = IndicatorsManager.BuildIn.MA(getEMAMTM, Second_S, MAMode.EMA);
            ema2_absmtm = IndicatorsManager.BuildIn.MA(getAbsEMAMTM, Second_S, MAMode.EMA);
        }

        private double getMTM(int index)
        {
            if (index + 1 < HistoryDataSeries.Count)
                return HistoryDataSeries.GetValue(PriceType.Close, index) - HistoryDataSeries.GetValue(PriceType.Close, index + 1);

            return index < HistoryDataSeries.Count ? HistoryDataSeries.GetValue(PriceType.Close, index) : 0D;
        }

        private double getAbsMTM(int index)
        {
            return Math.Abs(getMTM(index));
        }

        private double getEMAMTM(int index)
        {
            return ema_mtm.GetValue(index);
        }

        private double getAbsEMAMTM(int index)
        {
            return ema_absmtm.GetValue(index);
        }

        public override void Update(TickStatus args)
        {
            double divider = ema2_absmtm.GetValue();
            if(divider != 0)
                Lines["TSI"].SetValue(100 * ema2_mtm.GetValue() / ema2_absmtm.GetValue());
        }
    }
    #endregion

    #region (STOCHASTICxRSI)Stochastic x Relative Strength Index
    public class STOCHASTICxRSI : IndicatorBuilder
    {
        public STOCHASTICxRSI()
            : base()
        {
            Credentials.ProjectName = "Stochastic x Relativ Strength Index";

            Lines.Set("StochRSI");

            Lines.Set("Smoothing");
            Lines["Smoothing"].Color = Color.LightSteelBlue;

            Thresholds.Set("Up");
            Thresholds["Up"].Color = Color.Gray;
            Thresholds["Up"].Level = 80;
            Thresholds["Up"].Style = LineStyle.DotLine;

            Thresholds.Set("Down");
            Thresholds["Down"].Color = Color.Gray;
            Thresholds["Down"].Level = 20;
            Thresholds["Down"].Style = LineStyle.DotLine;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "RSI Period", 0)]
        [SimpleNumeric(2D, 99999D)]
        public int rsiPeriod = 14;

        [InputParameter(InputType.Combobox, "RSI Mode", 1)]
        [ComboboxItem("Simple", RSIMode.Simple)]
        [ComboboxItem("Exponential", RSIMode.Exponential)]
        public RSIMode rsiMode = RSIMode.Simple;

        [InputParameter(InputType.Combobox, "RSI source", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType sourcePrice = PriceType.Close;

        [InputParameter(InputType.Numeric, "Stochastic period", 3)]
        [SimpleNumeric(2D, 99999D)]
        public int stochasticPeriod = 15;

        [InputParameter(InputType.Numeric, "%K", 4)]
        [SimpleNumeric(1D, 99999D)]
        public int K = 3;

        [InputParameter(InputType.Numeric, "%D", 5)]
        [SimpleNumeric(1D, 99999D)]
        public int D = 3;

        [InputParameter(InputType.Combobox, "Smoothing Type", 6)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode mAType = MAMode.SMA;

        List<double> rsi = new List<double>();

        BuiltInIndicator smoothingLineK;

        BuiltInIndicator smoothingLineD;

        private double prevV = 0.0;
        private double prevP = 0.0;
        private double sumV = 0.0;
        private double sumP = 0.0;

        public override void Init()
        {
            ScriptShortName = $"StochRSI({rsiPeriod},{stochasticPeriod},{sourcePrice})";

            smoothingLineK = IndicatorsManager.BuildIn.MA(GetStochRSI, K, mAType);

            smoothingLineD = IndicatorsManager.BuildIn.MA(x => smoothingLineK.GetValue(x), D, mAType);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < rsiPeriod + stochasticPeriod + Math.Max(K, D))
            {
                rsi.Insert(0, 0);
                return;
            }                

            if (args != TickStatus.IsQuote)
            {
                rsi.Insert(0, 0);
                if (rsiMode == RSIMode.Simple)
                    rsi[0] = CalcSimple();
                else
                    rsi[0] = CalcExpon();
                prevV = sumV;
                prevP = sumP;
                return;
            }

            if (rsiMode == RSIMode.Simple)
                rsi[0] = CalcSimple();
            else
                rsi[0] = CalcExpon();

            Lines["StochRSI"].SetValue(smoothingLineK.GetValue());
            Lines["Smoothing"].SetValue(smoothingLineD.GetValue());
        }

        public double CalcSimple()
        {
            sumV = 0.0;
            sumP = 0.0;
            for (int i = 0; i < rsiPeriod; i++)
            {
                double diff = HistoryDataSeries.GetValue(sourcePrice, i) - HistoryDataSeries.GetValue(sourcePrice, i + 1);
                if (diff > 0.0)
                    sumV += diff;
                else
                    sumP -= diff;
            }

            double rsi = (sumP != 0) ? 100.0 * (1.0 - 1.0 / (1.0 + sumV / sumP)) : 100D;
            return rsi;
        }

        public double CalcExpon()
        {
            double rsi;
            if (HistoryDataSeries.Count == rsiPeriod + 1)
            {
                rsi = CalcSimple();
                prevV = sumV = (sumV / rsiPeriod);
                prevP = sumP = (sumP / rsiPeriod);
            }
            else
            {
                double diff = HistoryDataSeries.GetValue(sourcePrice) - HistoryDataSeries.GetValue(sourcePrice, 1);
                if (diff > 0)
                {
                    sumV = (prevV * (rsiPeriod - 1) + diff) / rsiPeriod;
                    sumP = (prevP * (rsiPeriod - 1)) / rsiPeriod;
                }
                else
                {
                    sumV = (prevV * (rsiPeriod - 1)) / rsiPeriod;
                    sumP = (prevP * (rsiPeriod - 1) - diff) / rsiPeriod;
                }
            }

            rsi = (sumP != 0) ? 100D - 100D / (1 + sumV / sumP) : 0D;
            return rsi;
        }

        private double GetStochRSI(int index = 0)
        {
            if (index + 1 > HistoryDataSeries.Count)
                return 0;

            double rsiValue, denominator, rsiMin = double.MaxValue, rsiMax = double.MinValue;

            for (int j = 0; j < stochasticPeriod; j++)
            {
                rsiValue = rsi.Count > index + j ? rsi[index + j] : rsi[index];

                if (rsiValue > rsiMax)
                    rsiMax = rsiValue;
                if (rsiValue < rsiMin)
                    rsiMin = rsiValue;
            }

            denominator = rsiMax - rsiMin;

            return denominator == 0 ? 0 : 100 * (rsi[index] - rsiMin) / denominator;
        }
    }
    #endregion
}
