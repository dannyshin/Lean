/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Scheduling;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm that test if the fill prices are the correct quote side.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class EquityTradeAndQuotesRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _symbol;
        private bool _canTrade;
        private int _quoteCounter;
        private int _tradeCounter;


        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            
            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));

            _symbol = AddEquity("IBM", Resolution.Minute).Symbol;

            // 2013-10-07 was Monday, that's why we ask 3 days history to get  data from previous Friday.
            var history = History(new [] {_symbol}, TimeSpan.FromDays(3), Resolution.Minute).ToList();
            Log($"{Time} - history.Count: {history.Count}");

            const int expectedSliceCount = 390;
            if (history.Count != expectedSliceCount)
            {
                throw new Exception($"History slices - expected: {expectedSliceCount}, actual: {history.Count}");
            }


            if (history.Any(s => s.Bars.Count != 1 && s.QuoteBars.Count != 1))
            {
                throw new Exception($"History not all slices have trades and quotes.");
            }

            Schedule.On(DateRules.EveryDay(_symbol), TimeRules.AfterMarketOpen(_symbol, 0), () => { _canTrade = true; });

            Schedule.On(DateRules.EveryDay(_symbol), TimeRules.BeforeMarketClose(_symbol, 16), () => { _canTrade = false; });

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            _quoteCounter += data.QuoteBars.Count;
            _tradeCounter += data.Bars.Count;

            if (!Portfolio.Invested && _canTrade)
            {
                SetHoldings(_symbol, 1);
                Log($"Purchased Security {_symbol.ID}");
            }

            if (Time.Minute % 15 == 0)
            {
                Liquidate();
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            var subscriptions = changes.AddedSecurities.First().Subscriptions;
            if (subscriptions.Count() != 2 &&
                subscriptions.Any(s=>s.TickType==TickType.Trade) &&
                subscriptions.Any(s=>s.TickType==TickType.Quote)
                )
            {
                throw new Exception($"Subscriptions were not correctly added.");
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status == OrderStatus.Filled)
            {
                Log($"{Time:s} {orderEvent.Direction}");
                var expectedFillPrice = orderEvent.Direction == OrderDirection.Buy ? Securities[_symbol].AskPrice : Securities[_symbol].BidPrice;
                if (orderEvent.FillPrice != expectedFillPrice)
                {
                    throw new Exception($"Fill price is not the expected for OrderId {orderEvent.OrderId} at Algorithm Time {Time:s}." +
                                        $"\n\tExpected fill price: {expectedFillPrice}, Actual fill price: {orderEvent.FillPrice}");
                }
            }
        }

        public override void OnEndOfAlgorithm()
        {
            // We expect 390 * 5 = 1950 trade bars. 
            if (_tradeCounter != 1950)
            {
                throw new Exception($"Fail at trade bars count expected: 1950, actual: {_tradeCounter}.");
            }
            // We expect 390 * 5 = 1950 quote bars. 
            if (_quoteCounter != 1950)
            {
                throw new Exception($"Fail at trade bars count expected: 1950, actual: {_quoteCounter}.");
            }

        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "250"},
            {"Average Win", "0.13%"},
            {"Average Loss", "-0.08%"},
            {"Compounding Annual Return", "-87.287%"},
            {"Drawdown", "3.400%"},
            {"Expectancy", "-0.293"},
            {"Net Profit", "-2.786%"},
            {"Sharpe Ratio", "-9.786"},
            {"Probabilistic Sharpe Ratio", "0.022%"},
            {"Loss Rate", "74%"},
            {"Win Rate", "26%"},
            {"Profit-Loss Ratio", "1.68"},
            {"Alpha", "-1.243"},
            {"Beta", "0.183"},
            {"Annual Standard Deviation", "0.12"},
            {"Annual Variance", "0.014"},
            {"Information Ratio", "-7.976"},
            {"Tracking Error", "0.192"},
            {"Treynor Ratio", "-6.443"},
            {"Total Fees", "$669.80"}
        };
    }
}