using System.Net;
using System.Net.Http;
using System.Text;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Infrastructure.Payments;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;
using Xunit;

namespace POSApp.Tests
{
    /// <summary>
    /// Stands in for Stripe's API: replies by "METHOD path" (query included), in order when a
    /// path is called more than once (the last reply repeats). Records every request.
    /// </summary>
    internal sealed class FakeStripe : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<(HttpStatusCode Status, string Body)>> _replies = new();

        public List<(string Key, string Form, string? Auth)> Requests { get; } = new();

        /// <summary>Called once a request is recorded (e.g. to cancel mid-charge).</summary>
        public Action<string>? OnRequest { get; set; }

        public FakeStripe Reply(string key, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            if (!_replies.TryGetValue(key, out var queue)) _replies[key] = queue = new();
            queue.Enqueue((status, body));
            return this;
        }

        public bool Called(string key) => Requests.Any(r => r.Key == key);

        public string FormOf(string key) => Requests.First(r => r.Key == key).Form;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var key = $"{request.Method} {request.RequestUri!.PathAndQuery.Replace("/v1/", "")}";
            var form = request.Content == null ? string.Empty : WebUtility.UrlDecode(await request.Content.ReadAsStringAsync(ct));
            Requests.Add((key, form, request.Headers.Authorization?.ToString()));
            OnRequest?.Invoke(key);

            if (!_replies.TryGetValue(key, out var queue))
                return Json(HttpStatusCode.NotFound, """{"error":{"message":"No such route in the fake."}}""");
            var (status, body) = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
            return Json(status, body);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <summary>The Stripe Terminal client, against a fake Stripe.</summary>
    public sealed class StripeTerminalTests
    {
        private const string Reader = "tmr_TEST1";

        private const string PaidCharge =
            """{"id":"pi_1","status":"succeeded","latest_charge":{"payment_method_details":{"card_present":{"brand":"visa","last4":"4242","receipt":{"authorization_code":"123456"}}}}}""";

        private static (StripeTerminal Terminal, FakeStripe Stripe) Terminal(string key = "sk_test_abc", int timeoutMs = 2000)
        {
            var stripe = new FakeStripe();
            var terminal = new StripeTerminal(new HttpClient(stripe), key, Reader,
                pollInterval: TimeSpan.FromMilliseconds(5), timeout: TimeSpan.FromMilliseconds(timeoutMs));
            return (terminal, stripe);
        }

        private static FakeStripe StartsCharge(FakeStripe stripe) => stripe
            .Reply("POST payment_intents", """{"id":"pi_1","status":"requires_payment_method"}""")
            .Reply($"POST terminal/readers/{Reader}/process_payment_intent", """{"id":"tmr_TEST1"}""")
            .Reply($"POST terminal/readers/{Reader}/cancel_action", """{"id":"tmr_TEST1"}""")
            .Reply("POST payment_intents/pi_1/cancel", """{"id":"pi_1","status":"canceled"}""");

        [Fact]
        public async Task Charge_ShowsTheAmountOnTheReader_CapturesIt_AndReadsTheCard()
        {
            var (terminal, stripe) = Terminal();
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"in_progress"}}""")
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"succeeded"}}""")
                .Reply("GET payment_intents/pi_1?expand[]=latest_charge", """{"id":"pi_1","status":"requires_capture"}""")
                .Reply("POST payment_intents/pi_1/capture", PaidCharge);

            var result = await terminal.ChargeAsync(12.16m, "Sale 11050");

            Assert.True(result.Approved);
            Assert.Equal("pi_1", result.PaymentReference);
            Assert.Equal("Visa", result.Brand);
            Assert.Equal("4242", result.Last4);
            Assert.Equal("123456", result.AuthCode);

            var intent = stripe.FormOf("POST payment_intents");
            Assert.Contains("amount=1216", intent);
            Assert.Contains("currency=usd", intent);
            Assert.Contains("payment_method_types[]=card_present", intent);
            Assert.Contains("description=Sale 11050", intent);
            Assert.Equal("payment_intent=pi_1", stripe.FormOf($"POST terminal/readers/{Reader}/process_payment_intent"));
            Assert.All(stripe.Requests, r => Assert.Equal("Bearer sk_test_abc", r.Auth));
            Assert.False(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task Charge_AlreadyCaptured_IsNotCapturedAgain()
        {
            var (terminal, stripe) = Terminal();
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"succeeded"}}""")
                .Reply("GET payment_intents/pi_1?expand[]=latest_charge", PaidCharge);

            var result = await terminal.ChargeAsync(5m, "Sale");

            Assert.True(result.Approved);
            Assert.False(stripe.Called("POST payment_intents/pi_1/capture"));
        }

        [Fact]
        public async Task Charge_ReaderSucceeded_ButPaymentDidNot_ReleasesTheCard()
        {
            var (terminal, stripe) = Terminal();
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"succeeded"}}""")
                .Reply("GET payment_intents/pi_1?expand[]=latest_charge", """{"id":"pi_1","status":"processing"}""");

            var result = await terminal.ChargeAsync(20m, "Sale");

            Assert.False(result.Approved);
            Assert.Contains("processing", result.Message);
            Assert.True(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task Charge_ReaderCallFails_CancelsThePaymentBeforeReporting()
        {
            var (terminal, stripe) = Terminal();
            stripe.Reply("POST payment_intents", """{"id":"pi_1","status":"requires_payment_method"}""")
                .Reply($"POST terminal/readers/{Reader}/process_payment_intent", """{"error":{"message":"Reader is offline."}}""", HttpStatusCode.BadRequest)
                .Reply($"POST terminal/readers/{Reader}/cancel_action", """{"id":"tmr_TEST1"}""")
                .Reply("POST payment_intents/pi_1/cancel", """{"id":"pi_1","status":"canceled"}""");

            var ex = await Assert.ThrowsAsync<StripeException>(() => terminal.ChargeAsync(20m, "Sale"));

            Assert.Equal("Reader is offline.", ex.Message);
            Assert.True(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task Charge_Declined_ReportsWhy_AndCancelsThePayment()
        {
            var (terminal, stripe) = Terminal();
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"failed","failure_message":"Your card has insufficient funds."}}""");

            var result = await terminal.ChargeAsync(20m, "Sale");

            Assert.False(result.Approved);
            Assert.Equal("Your card has insufficient funds.", result.Message);
            Assert.True(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task Charge_NoCardInTime_ClearsTheReader()
        {
            var (terminal, stripe) = Terminal(timeoutMs: 40);
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"in_progress"}}""");

            var result = await terminal.ChargeAsync(20m, "Sale");

            Assert.False(result.Approved);
            Assert.Equal("No card was presented in time.", result.Message);
            Assert.True(stripe.Called($"POST terminal/readers/{Reader}/cancel_action"));
            Assert.True(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task Charge_CancelledByTheCashier_ClearsTheReader()
        {
            var (terminal, stripe) = Terminal();
            StartsCharge(stripe)
                .Reply($"GET terminal/readers/{Reader}", """{"action":{"status":"in_progress"}}""");
            using var cts = new CancellationTokenSource();
            stripe.OnRequest = key => { if (key == $"GET terminal/readers/{Reader}") cts.Cancel(); };

            var result = await terminal.ChargeAsync(20m, "Sale", cts.Token);

            Assert.False(result.Approved);
            Assert.Equal("Cancelled.", result.Message);
            Assert.True(stripe.Called($"POST terminal/readers/{Reader}/cancel_action"));
            Assert.True(stripe.Called("POST payment_intents/pi_1/cancel"));
        }

        [Fact]
        public async Task StripeError_IsShownInItsOwnWords()
        {
            var (terminal, stripe) = Terminal();
            stripe.Reply("POST payment_intents", """{"error":{"message":"Invalid API Key provided: sk_test_***abc"}}""", HttpStatusCode.Unauthorized);

            var ex = await Assert.ThrowsAsync<StripeException>(() => terminal.ChargeAsync(1m, "Sale"));
            Assert.Equal("Invalid API Key provided: sk_test_***abc", ex.Message);
        }

        [Fact]
        public async Task NothingToCharge_NeverCallsStripe()
        {
            var (terminal, stripe) = Terminal();
            var result = await terminal.ChargeAsync(0m, "Sale");
            Assert.False(result.Approved);
            Assert.Empty(stripe.Requests);
        }

        [Theory]
        [InlineData("succeeded", true, null)]
        [InlineData("pending", true, "The refund is on its way to the card.")]
        [InlineData("failed", false, "The refund was not accepted (failed).")]
        public async Task Refund_ReportsTheOutcome(string status, bool succeeded, string? message)
        {
            var (terminal, stripe) = Terminal();
            stripe.Reply("POST refunds", $$"""{"id":"re_1","status":"{{status}}"}""");

            var result = await terminal.RefundAsync("pi_1", 7.5m);

            Assert.Equal(succeeded, result.Succeeded);
            Assert.Equal(message, result.Message);
            Assert.Equal("payment_intent=pi_1&amount=750", stripe.FormOf("POST refunds"));
        }

        [Fact]
        public async Task Refund_RefusedByStripe_DoesNotThrow()
        {
            var (terminal, stripe) = Terminal();
            stripe.Reply("POST refunds", """{"error":{"message":"Refund amount is greater than unrefunded amount on charge."}}""", HttpStatusCode.BadRequest);

            var result = await terminal.RefundAsync("pi_1", 99m);

            Assert.False(result.Succeeded);
            Assert.Equal("Refund amount is greater than unrefunded amount on charge.", result.Message);
        }

        [Fact]
        public async Task TestCards_AndTestReaders_NeedTestKeys()
        {
            var (live, _) = Terminal("sk_live_abc");
            Assert.False(live.IsTestMode);
            await Assert.ThrowsAsync<InvalidOperationException>(() => live.SimulateCardAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => live.CreateSimulatedReaderAsync());

            var (test, stripe) = Terminal("sk_test_abc");
            Assert.True(test.IsTestMode);
            stripe.Reply($"POST test_helpers/terminal/readers/{Reader}/present_payment_method", """{"id":"tmr_TEST1"}""");
            await test.SimulateCardAsync();
            Assert.True(stripe.Called($"POST test_helpers/terminal/readers/{Reader}/present_payment_method"));
        }

        [Fact]
        public async Task ListReaders_ReadsLabelsAndStatus()
        {
            var (terminal, stripe) = Terminal();
            stripe.Reply("GET terminal/readers?limit=100",
                """{"data":[{"id":"tmr_A","label":"Front counter","status":"online"},{"id":"tmr_B","device_type":"bbpos_wisepos_e","status":"offline"}]}""");

            var readers = await terminal.ListReadersAsync();

            Assert.Equal(new[] { ("tmr_A", "Front counter", true), ("tmr_B", "bbpos_wisepos_e", false) }, readers);
        }

        [Theory]
        [InlineData("12.16", 1216)]
        [InlineData("0.005", 1)]
        [InlineData("12.345", 1235)]
        [InlineData("100", 10000)]
        public void Cents_RoundHalfAwayFromZero(string amount, long cents) =>
            Assert.Equal(cents, StripeTerminal.ToCents(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>The reader set-up kept in the database, with the key encrypted for this PC.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class CardTerminalSettingsTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        [Fact]
        public void Key_IsEncrypted_AndComesBack()
        {
            var stored = CardTerminalSettingsStore.Protect("sk_live_secret");
            Assert.DoesNotContain("sk_live", stored);
            Assert.Equal("sk_live_secret", CardTerminalSettingsStore.Unprotect(stored));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("bm90IGEgZHBhcGkgYmxvYg==")]   // from another PC, or damaged
        [InlineData("not base64 at all")]
        public void UnreadableKey_MeansEnterItAgain(string? stored) =>
            Assert.Null(CardTerminalSettingsStore.Unprotect(stored));

        [Fact]
        public async Task NewDatabase_HasNoReader()
        {
            using var db = _dir.NewMigratedContext();
            var settings = await new CardTerminalSettingsStore(db).GetAsync();
            Assert.Equal(CardTerminalSettings.None, settings.Provider);
            Assert.False(settings.IsStripe);
            Assert.Null(await new CardTerminalFactory(new CardTerminalSettingsStore(db)).CreateAsync());
        }

        [Fact]
        public async Task SavedReader_IsUsed_AndTheKeyIsNotStoredInPlainText()
        {
            using (var db = _dir.NewMigratedContext())
            {
                var store = new CardTerminalSettingsStore(db);
                await store.SaveAsync(new CardTerminalSettings(CardTerminalSettings.StripeTerminal, " sk_test_secret ", "tmr_A"));
                await store.SaveAsync(new CardTerminalSettings(CardTerminalSettings.StripeTerminal, "sk_test_secret", "tmr_B"));   // updates, no duplicates
            }

            using (var db = _dir.NewMigratedContext())
            {
                var rows = db.ApplicationSettings.Where(s => s.Key.StartsWith("Payments.")).ToList();
                Assert.Equal(3, rows.Count);
                Assert.DoesNotContain(rows, r => r.Value.Contains("sk_test"));

                var settings = await new CardTerminalSettingsStore(db).GetAsync();
                Assert.True(settings.IsStripe);
                Assert.True(settings.IsTestKey);
                Assert.Equal("sk_test_secret", settings.ApiKey);
                Assert.Equal("tmr_B", settings.ReaderId);

                var terminal = await new CardTerminalFactory(new CardTerminalSettingsStore(db)).CreateAsync();
                Assert.NotNull(terminal);
                Assert.True(terminal!.IsTestMode);
            }
        }

        [Fact]
        public async Task SwitchedBackToSeparateMachine_NoReaderIsUsed()
        {
            using var db = _dir.NewMigratedContext();
            var store = new CardTerminalSettingsStore(db);
            await store.SaveAsync(new CardTerminalSettings(CardTerminalSettings.StripeTerminal, "sk_test_secret", "tmr_A"));
            await store.SaveAsync(new CardTerminalSettings(CardTerminalSettings.None, "sk_test_secret", "tmr_A"));

            Assert.Null(await new CardTerminalFactory(store).CreateAsync());
        }
    }

    /// <summary>Checkout and returns with an integrated card reader.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class CardReaderCheckoutTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public CardReaderCheckoutTests() => Region.Save(RegionSettingsData.UnitedStates());

        public void Dispose() => Region.Save(_original);

        private static readonly Product Bandages = new() { Id = 1, ProductId = "P1", ProductName = "Adhesive Bandages", UnitPrice = 10m, Stock = 50 };
        private static readonly Product Gauze = new() { Id = 2, ProductId = "P2", ProductName = "Gauze Pads", UnitPrice = 4m, Stock = 50 };

        /// <summary>A US sale screen with a reader that approves every charge (or declines all).</summary>
        private sealed class Till
        {
            public SaleViewModel Vm { get; }
            public List<Sale> Saved { get; } = new();
            public List<(decimal Amount, string Description)> Charges { get; } = new();
            public List<(string Reference, decimal Amount)> Voids { get; } = new();
            public List<(string Message, bool Refunded)> Notices { get; } = new();
            public bool Decline { get; set; }
            public bool VoidFails { get; set; }

            public Till()
            {
                var tax = new Mock<ITaxRepository>();
                tax.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TaxSettings.Off);
                Vm = SaleFixtures.NewSaleScreen(saved: Saved, tax: tax.Object, catalogue: new[] { Bandages, Gauze });
                Vm.RequestCardCharge = (amount, description) =>
                {
                    Charges.Add((amount, description));
                    var n = Charges.Count;
                    return Decline ? null : new CardChargeResult(true, $"pi_{n}", "Visa", "4242", $"AUTH{n}", null);
                };
                Vm.VoidCardCharge = (reference, amount) =>
                {
                    Voids.Add((reference, amount));
                    return Task.FromResult(!VoidFails);
                };
                Vm.CardNotice = (message, _, refunded) => Notices.Add((message, refunded));
                Vm.AddProductToCart(Bandages);
            }

            public bool Checkout() =>
                Vm.ResolvePayments() && Vm.ChargeCardsOnReaderAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public void CardSale_IsChargedOnTheReader_AndSavedWithTheCardDetails()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.PaymentType = PaymentMethods.Card;

                till.Vm.SaveCommand.Execute(null);

                var charge = Assert.Single(till.Charges);
                Assert.Equal(10m, charge.Amount);
                Assert.Equal("Sale 11050", charge.Description);
                var payment = Assert.Single(Assert.Single(till.Saved).Payments);
                Assert.Equal(PaymentMethods.Card, payment.Method);
                Assert.Equal("pi_1", payment.ProcessorReference);
                Assert.Equal("Visa", payment.CardBrand);
                Assert.Equal("4242", payment.CardLast4);
                Assert.Equal("AUTH1", payment.Reference);
                Assert.False(till.Vm.HasUnsavedCardCharges);
                Assert.Empty(till.Voids);
            });
        }

        [Fact]
        public void DeclinedCard_SavesNothing()
        {
            Sta.Run(() =>
            {
                var till = new Till { Decline = true };
                till.Vm.PaymentType = PaymentMethods.Card;

                till.Vm.SaveCommand.Execute(null);

                Assert.Single(till.Charges);
                Assert.Empty(till.Saved);
                Assert.False(till.Vm.HasUnsavedCardCharges);
            });
        }

        [Fact]
        public void CashSale_NeverTouchesTheReader()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.PaymentType = PaymentMethods.Cash;
                till.Vm.SaveCommand.Execute(null);
                Assert.Empty(till.Charges);
                Assert.Single(till.Saved);
            });
        }

        [Fact]
        public void TryingAgain_AfterAFailedPrint_DoesNotChargeTwice()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.PaymentType = PaymentMethods.Card;

                Assert.True(till.Checkout());
                Assert.True(till.Vm.HasUnsavedCardCharges);
                Assert.True(till.Checkout());   // e.g. the printer was out of paper

                Assert.Single(till.Charges);
                Assert.Equal("pi_1", Assert.Single(till.Vm.Payments).ProcessorReference);
                Assert.Empty(till.Voids);
            });
        }

        [Fact]
        public void BillChangedAfterCharging_RefundsTheOldChargeAndChargesTheNewTotal()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.PaymentType = PaymentMethods.Card;
                Assert.True(till.Checkout());

                till.Vm.AddProductToCart(Gauze);   // 10.00 -> 14.00
                Assert.True(till.Checkout());

                Assert.Equal(new[] { 10m, 14m }, till.Charges.Select(c => c.Amount));
                Assert.Equal(("pi_1", 10m), Assert.Single(till.Voids));
                Assert.True(Assert.Single(till.Notices).Refunded);
                Assert.Equal("pi_2", Assert.Single(till.Vm.Payments).ProcessorReference);
            });
        }

        [Fact]
        public void AbandonedBill_RefundsItsCharge_AndSaysSoIfThatFails()
        {
            Sta.Run(() =>
            {
                var till = new Till { VoidFails = true };
                till.Vm.PaymentType = PaymentMethods.Card;
                Assert.True(till.Checkout());

                till.Vm.NewCommand.Execute(null);

                Assert.Equal(("pi_1", 10m), Assert.Single(till.Voids));
                var notice = Assert.Single(till.Notices);
                Assert.False(notice.Refunded);
                Assert.Contains("Stripe dashboard", notice.Message);
                Assert.False(till.Vm.HasUnsavedCardCharges);
            });
        }

        [Fact]
        public void SplitPayment_ChargesOnlyTheCardPart()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.ApplySplitPayments(new[]
                {
                    new SalePayment { Method = PaymentMethods.Cash, Amount = 4m, Tendered = 4m },
                    new SalePayment { Method = PaymentMethods.Card, Amount = 6m, Tendered = 6m, CardBrand = "FSA/HSA card" }
                });

                till.Vm.SaveCommand.Execute(null);

                Assert.Equal(6m, Assert.Single(till.Charges).Amount);
                var card = Assert.Single(till.Saved).Payments.Single(p => p.Method == PaymentMethods.Card);
                Assert.Equal("pi_1", card.ProcessorReference);
                Assert.Equal("Visa", card.CardBrand);
            });
        }

        [Fact]
        public void NoReader_CardIsRecordedAsBefore()
        {
            Sta.Run(() =>
            {
                var till = new Till();
                till.Vm.RequestCardCharge = null;
                till.Vm.PaymentType = PaymentMethods.Card;

                till.Vm.SaveCommand.Execute(null);

                var payment = Assert.Single(Assert.Single(till.Saved).Payments);
                Assert.Equal(PaymentMethods.Card, payment.Method);
                Assert.Null(payment.ProcessorReference);
            });
        }

        private static Sale ReaderSale()
        {
            var sale = new Sale { InvoiceNumber = "11017", TotalBill = 15.68m };
            sale.Payments.Add(new SalePayment
            {
                Method = PaymentMethods.Card, Amount = 15.68m, Tendered = 15.68m,
                CardBrand = "Visa", CardLast4 = "4242", Reference = "A1B2C3", ProcessorReference = "pi_1"
            });
            return sale;
        }

        private static (SaleReturnViewModel Vm, List<(string Reference, decimal Amount)> Calls) ReturnScreen(
            CardRefundResult? reply, bool giveCashInstead = true)
        {
            var calls = new List<(string, decimal)>();
            var vm = new SaleReturnViewModel(new Mock<ISaleRepository>().Object, new Mock<IProductRepository>().Object)
            {
                TotalReturnAmount = 8.11m,
                Confirm = (_, _) => giveCashInstead
            };
            if (reply != null)
                vm.RefundToCard = (reference, amount) => { calls.Add((reference, amount)); return Task.FromResult(reply); };
            return (vm, calls);
        }

        [Fact]
        public void Return_TriedAgain_AfterTheCardWasRefunded_DoesNotRefundTwice()
        {
            Sta.Run(() =>
            {
                var (vm, calls) = ReturnScreen(new CardRefundResult(true, "re_1", null));

                var first = vm.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult();
                var second = vm.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult();

                Assert.Same(first, second);
                Assert.Single(calls);
            });
        }

        [Fact]
        public void StockUpdateFails_AfterTheCardSaleIsSaved_DoesNotChargeTheCardAgain()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var charges = new List<decimal>();
                var vm = SaleFixtures.NewSaleScreen(
                    saved: saved,
                    catalogue: new[] { Bandages },
                    onProductUpdate: () => throw new InvalidOperationException("disk full"));
                vm.RequestCardCharge = (amount, _) =>
                {
                    charges.Add(amount);
                    return new CardChargeResult(true, "pi_1", "Visa", "4242", "AUTH1", null);
                };
                vm.VoidCardCharge = (_, _) => Task.FromResult(true);
                vm.CardNotice = (_, _, _) => { };
                vm.AddProductToCart(Bandages);
                vm.PaymentType = PaymentMethods.Card;

                vm.SaveCommand.Execute(null);

                Assert.Single(saved);
                Assert.Equal(10m, Assert.Single(charges));
                Assert.Empty(vm.SaleItems);
                Assert.False(vm.HasUnsavedCardCharges);

                vm.SaveCommand.Execute(null);
                Assert.Single(saved);
                Assert.Single(charges);
            });
        }

        [Fact]
        public void Return_OfAReaderSale_IsRefundedToTheSameCard()
        {
            Sta.Run(() =>
            {
                var (vm, calls) = ReturnScreen(new CardRefundResult(true, "re_1", null));

                var refund = vm.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult()!;

                Assert.Equal(("pi_1", 8.11m), Assert.Single(calls));
                Assert.Equal(PaymentMethods.Card, refund.Method);
                Assert.Equal(-8.11m, refund.Amount);
                Assert.Equal("re_1", refund.ProcessorReference);
                Assert.Equal("Card (Visa ****4242)", TenderCalculator.Describe(refund));
            });
        }

        [Fact]
        public void CardRefundFailed_CashierCanGiveCashInstead_OrStop()
        {
            Sta.Run(() =>
            {
                var declined = new CardRefundResult(false, null, "Charge already refunded.");

                var cash = ReturnScreen(declined, giveCashInstead: true).Vm.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult()!;
                Assert.Equal(PaymentMethods.Cash, cash.Method);
                Assert.Null(cash.ProcessorReference);
                Assert.Equal("Cash", TenderCalculator.Describe(cash));

                Assert.Null(ReturnScreen(declined, giveCashInstead: false).Vm.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult());
            });
        }

        [Fact]
        public void NoReader_OrASaleKeyedOnASeparateMachine_RecordsTheRefundAsBefore()
        {
            Sta.Run(() =>
            {
                var (noReader, _) = ReturnScreen(null);
                var refund = noReader.TakeRefundAsync(ReaderSale()).GetAwaiter().GetResult()!;
                Assert.Equal(PaymentMethods.Card, refund.Method);
                Assert.Null(refund.ProcessorReference);

                var keyed = ReaderSale();
                keyed.Payments.Single().ProcessorReference = null;
                var (vm, calls) = ReturnScreen(new CardRefundResult(true, "re_1", null));
                Assert.Equal(PaymentMethods.Card, vm.TakeRefundAsync(keyed).GetAwaiter().GetResult()!.Method);
                Assert.Empty(calls);
            });
        }

        [Fact]
        public void Refund_GoesToTheReaderCard_OnlyWhenTheSaleHadExactlyOne()
        {
            static Sale With(params SalePayment[] payments)
            {
                var sale = new Sale();
                foreach (var p in payments) sale.Payments.Add(p);
                return sale;
            }

            var reader = new SalePayment { Method = PaymentMethods.Card, Amount = 10m, ProcessorReference = "pi_1" };
            Assert.Same(reader, SaleReturnViewModel.ReaderCardPayment(With(reader)));
            Assert.Same(reader, SaleReturnViewModel.ReaderCardPayment(With(reader, new SalePayment { Method = PaymentMethods.Cash, Amount = 2m })));
            Assert.Null(SaleReturnViewModel.ReaderCardPayment(With(new SalePayment { Method = PaymentMethods.Card, Amount = 10m })));
            Assert.Null(SaleReturnViewModel.ReaderCardPayment(With(reader, new SalePayment { Method = PaymentMethods.Card, Amount = 3m, ProcessorReference = "pi_2" })));
            Assert.Null(SaleReturnViewModel.ReaderCardPayment(With()));
        }
    }
}
