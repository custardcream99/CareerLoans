// CareerLoans — KSP1 (1.8–1.12) loan mod (net472)
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using KSP;
using KSP.UI.Screens;

namespace CareerLoans
{
    // =========================
    // Difficulty Options (per-save) — single-arg UI attributes + code clamping
    // =========================
    public class CareerLoansSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "Career Loans";
        public override string Section => "Mods";
        public override string DisplaySection => "Mods";
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.CAREER;
        public override bool HasPresets => true;

        // Reputation thresholds
        [GameParameters.CustomIntParameterUI("Rep for Tier 2")]
        public int RepTier2 = 200;

        [GameParameters.CustomIntParameterUI("Rep for Tier 3")]
        public int RepTier3 = 300;

        // Max concurrent loans per tier
        [GameParameters.CustomIntParameterUI("Max Loans (Tier 1)")]
        public int MaxLoansTier1 = 1;

        [GameParameters.CustomIntParameterUI("Max Loans (Tier 2)")]
        public int MaxLoansTier2 = 2;

        [GameParameters.CustomIntParameterUI("Max Loans (Tier 3)")]
        public int MaxLoansTier3 = 3;

        // Max principal per tier (floats)
        [GameParameters.CustomFloatParameterUI("Max Principal (Tier 1)")]
        public float MaxPrincipalTier1 = 500_000f;

        [GameParameters.CustomFloatParameterUI("Max Principal (Tier 2)")]
        public float MaxPrincipalTier2 = 2_000_000f;

        [GameParameters.CustomFloatParameterUI("Max Principal (Tier 3)")]
        public float MaxPrincipalTier3 = 10_000_000f;

        // Max term (months)
        [GameParameters.CustomIntParameterUI("Max Term (months)")]
        public int MaxTermMonths = 120;

        // APR clamps
        [GameParameters.CustomFloatParameterUI("APR Min")]
        public float APRMin = 0.01f;

        [GameParameters.CustomFloatParameterUI("APR Max")]
        public float APRMax = 0.25f;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    RepTier2 = 150; RepTier3 = 250;
                    APRMin = 0.01f; APRMax = 0.20f;
                    MaxTermMonths = 120;
                    break;

                case GameParameters.Preset.Normal:   // treat Normal as our standard defaults
                    RepTier2 = 200; RepTier3 = 300;
                    APRMin = 0.01f; APRMax = 0.25f;
                    MaxTermMonths = 120;
                    break;

                case GameParameters.Preset.Hard:
                    RepTier2 = 220; RepTier3 = 330;
                    APRMin = 0.02f; APRMax = 0.30f;
                    MaxTermMonths = 120;
                    break;

                default: // any other/unknown preset -> use Normal defaults
                    RepTier2 = 200; RepTier3 = 300;
                    APRMin = 0.01f; APRMax = 0.25f;
                    MaxTermMonths = 120;
                    break;
            }

            ClampAll();
        }

        // Centralized clamping for older UI attribs with no min/max support
        public void ClampAll()
        {
            RepTier2 = Mathf.Clamp(RepTier2, 0, 1000);
            RepTier3 = Mathf.Clamp(RepTier3, 0, 1000);
            if (RepTier3 < RepTier2) RepTier3 = RepTier2;

            MaxLoansTier1 = Mathf.Clamp(MaxLoansTier1, 0, 10);
            MaxLoansTier2 = Mathf.Clamp(MaxLoansTier2, 0, 10);
            MaxLoansTier3 = Mathf.Clamp(MaxLoansTier3, 0, 10);

            MaxPrincipalTier1 = Mathf.Clamp(MaxPrincipalTier1, 0f, 1_000_000_000f);
            MaxPrincipalTier2 = Mathf.Clamp(MaxPrincipalTier2, 0f, 1_000_000_000f);
            MaxPrincipalTier3 = Mathf.Clamp(MaxPrincipalTier3, 0f, 1_000_000_000f);

            MaxTermMonths = Mathf.Clamp(MaxTermMonths, 1, 240);

            APRMin = Mathf.Clamp(APRMin, 0f, 1f);
            APRMax = Mathf.Clamp(APRMax, 0f, 1f);
            if (APRMax < APRMin) APRMax = APRMin;
        }

        // Safe accessor that also clamps
        public static CareerLoansSettings Get()
        {
            var s = HighLogic.CurrentGame?.Parameters?.CustomParams<CareerLoansSettings>() ?? new CareerLoansSettings();
            s.ClampAll();
            return s;
        }
    }

    // =========================
    // Loan + Scenario (persistence + processing)
    // =========================
    [Serializable]
    public class Loan
    {
        public Guid Id;
        public double Principal;
        public double APR;
        public int TermMonths;
        public double MonthlyPayment;
        public int PaymentsMade;
        public double Remaining;
        public double StartUT;
        public double NextPaymentUT;

        public void Save(ConfigNode node)
        {
            node.AddValue("Id", Id.ToString());
            node.AddValue("Principal", Principal);
            node.AddValue("APR", APR);
            node.AddValue("TermMonths", TermMonths);
            node.AddValue("MonthlyPayment", MonthlyPayment);
            node.AddValue("PaymentsMade", PaymentsMade);
            node.AddValue("Remaining", Remaining);
            node.AddValue("StartUT", StartUT);
            node.AddValue("NextPaymentUT", NextPaymentUT);
        }

        public static Loan Load(ConfigNode node)
        {
            var l = new Loan();
            Guid.TryParse(node.GetValue("Id"), out l.Id);
            double.TryParse(node.GetValue("Principal"), out l.Principal);
            double.TryParse(node.GetValue("APR"), out l.APR);
            int.TryParse(node.GetValue("TermMonths"), out l.TermMonths);
            double.TryParse(node.GetValue("MonthlyPayment"), out l.MonthlyPayment);
            int.TryParse(node.GetValue("PaymentsMade"), out l.PaymentsMade);
            double.TryParse(node.GetValue("Remaining"), out l.Remaining);
            double.TryParse(node.GetValue("StartUT"), out l.StartUT);
            double.TryParse(node.GetValue("NextPaymentUT"), out l.NextPaymentUT);
            if (l.Id == Guid.Empty) l.Id = Guid.NewGuid();
            return l;
        }
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR)]
    public class LoanScenario : ScenarioModule
    {
        public static LoanScenario Instance;
        public readonly List<Loan> ActiveLoans = new List<Loan>();

        public const double SecondsPerKerbinDay = 6 * 3600.0;
        public const double SecondsPerMonth = 30 * SecondsPerKerbinDay;

        private double _lastCheckUT = 0;
        private const double CheckInterval = 5.0;

        public override void OnAwake()
        {
            Instance = this;
            base.OnAwake();
        }

        public override void OnLoad(ConfigNode node)
        {
            ActiveLoans.Clear();
            foreach (var ln in node.GetNodes("Loan"))
                ActiveLoans.Add(Loan.Load(ln));
            base.OnLoad(node);
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (var l in ActiveLoans)
            {
                var ln = new ConfigNode("Loan");
                l.Save(ln);
                node.AddNode(ln);
            }
            base.OnSave(node);
        }

        public void FixedUpdate()
        {
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return;

            var s = HighLogic.LoadedScene;
            if (s != GameScenes.FLIGHT && s != GameScenes.SPACECENTER && s != GameScenes.TRACKSTATION && s != GameScenes.EDITOR)
                return;

            double now = Planetarium.GetUniversalTime();
            if (now < _lastCheckUT + CheckInterval) return;
            _lastCheckUT = now;
            ProcessPayments(now);
        }

        private void ProcessPayments(double nowUT)
        {
            for (int i = ActiveLoans.Count - 1; i >= 0; i--)
            {
                var loan = ActiveLoans[i];
                while (loan.PaymentsMade < loan.TermMonths && nowUT >= loan.NextPaymentUT)
                {
                    double monthlyRate = loan.APR / 12.0;
                    double remainingBalance = RemainingBalance(loan);
                    double interest = remainingBalance * monthlyRate;
                    double principal = Math.Max(0, loan.MonthlyPayment - interest);

                    double currentFunds = Funding.Instance.Funds;
                    double amountToDeduct = loan.MonthlyPayment;

                    if (currentFunds >= amountToDeduct)
                    {
                        Funding.Instance.AddFunds(-amountToDeduct, TransactionReasons.None);
                    }
                    else
                    {
                        Funding.Instance.AddFunds(-currentFunds, TransactionReasons.None);
                        principal = Math.Max(0, principal - (amountToDeduct - currentFunds));
                    }

                    loan.PaymentsMade++;
                    loan.Remaining = Math.Max(0, remainingBalance - principal);
                    loan.NextPaymentUT += SecondsPerMonth;
                }

                if (loan.PaymentsMade >= loan.TermMonths || loan.Remaining <= 0.01)
                    ActiveLoans.RemoveAt(i);
            }
        }

        public static double PaymentFor(double principal, double apr, int termMonths)
        {
            double r = apr / 12.0;
            if (Math.Abs(r) < 1e-12) return principal / termMonths;
            double denom = 1.0 - Math.Pow(1.0 + r, -termMonths);
            return principal * (r / denom);
        }

        public static double RemainingBalance(Loan loan)
        {
            double P = loan.Principal;
            double r = loan.APR / 12.0;
            int k = loan.PaymentsMade;
            if (Math.Abs(r) < 1e-12)
            {
                double paid = k * loan.MonthlyPayment;
                return Math.Max(0, P - paid);
            }
            double pow = Math.Pow(1 + r, k);
            double balance = P * pow - loan.MonthlyPayment * ((pow - 1) / r);
            return Math.Max(0, balance);
        }

        public double ComputeAPRFromReputation()
        {
            var s = CareerLoansSettings.Get();

            double rep = Reputation.Instance != null ? Reputation.Instance.reputation : 0.0;
            rep = Math.Max(0.0, Math.Min(1000.0, rep));
            // Base curve: 20% at 0 rep → 2% at 1000 rep
            double apr = 0.20 - (rep / 1000.0) * (0.20 - 0.02);
            // Clamp to settings
            apr = Math.Max(s.APRMin, Math.Min(s.APRMax, apr));
            return apr;
        }

        public void GetLoanCapsFromReputation(out int maxLoans, out double maxPrincipal)
        {
            var s = CareerLoansSettings.Get();
            double rep = Reputation.Instance != null ? Reputation.Instance.reputation : 0.0;

            if (rep >= s.RepTier3)
            {
                maxLoans = s.MaxLoansTier3;
                maxPrincipal = s.MaxPrincipalTier3;
            }
            else if (rep >= s.RepTier2)
            {
                maxLoans = s.MaxLoansTier2;
                maxPrincipal = s.MaxPrincipalTier2;
            }
            else
            {
                maxLoans = s.MaxLoansTier1;
                maxPrincipal = s.MaxPrincipalTier1;
            }
        }

        // =========================
        // Early payoff
        // =========================
        public void PayOffLoan(Loan loan)
        {
            if (loan == null) return;

            // Remaining balance as of now
            double remaining = RemainingBalance(loan);

            if (remaining <= 0.01)
            {
                ActiveLoans.Remove(loan);
                return;
            }

            double funds = Funding.Instance != null ? Funding.Instance.Funds : 0.0;
            if (funds < remaining)
            {
                ScreenMessages.PostScreenMessage(
                    "Not enough funds to pay off this loan.",
                    3f, ScreenMessageStyle.UPPER_CENTER
                );
                return;
            }

            // Deduct funds and close the loan
            Funding.Instance.AddFunds(-remaining, TransactionReasons.None);
            ActiveLoans.Remove(loan);

            ScreenMessages.PostScreenMessage(
                $"Loan {loan.Id.ToString().Substring(0, 8)} paid off (−{remaining.ToString("N0", CultureInfo.InvariantCulture)}).",
                4f, ScreenMessageStyle.UPPER_CENTER
            );
        }
    }

    // =========================
    // UI (singleton + persistent; no duplicate buttons)
    // =========================
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class LoanUI : MonoBehaviour
    {
        public static LoanUI Instance;

        private Rect windowRect = new Rect(200, 100, 360, 560);
        private const string WindowTitle = "Career Loans";
        private bool visible = false;

        private double loanAmount = 100000;
        private int termMonths = 24;
        private Vector2 loansScroll;
        private ApplicationLauncherButton stockBtn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this);

            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
            RemoveButton();
            if (Instance == this) Instance = null;
        }

        private void OnAppLauncherReady()
        {
            if (ApplicationLauncher.Instance == null || stockBtn != null) return;

            stockBtn = ApplicationLauncher.Instance.AddModApplication(
                Toggle, Toggle, null, null, null, null,
                ApplicationLauncher.AppScenes.SPACECENTER |
                ApplicationLauncher.AppScenes.TRACKSTATION,
                MakeIcon()
            );
        }

        private void OnAppLauncherDestroyed()
        {
            stockBtn = null; // AppLauncher rebuilt; allow clean re-add
        }

        private void RemoveButton()
        {
            if (stockBtn != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(stockBtn);
            stockBtn = null;
        }

        private void OnSceneChange(GameScenes s)
        {
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
                visible = false;
        }

        private void Toggle() => visible = !visible;

        private Texture2D MakeIcon()
        {
            // Expecting GameData/CareerLoans/Icons/loan_icon.png (no extension in path)
            var tex = GameDatabase.Instance.GetTexture("CareerLoans/Icons/loan_icon", false);
            if (tex != null) return tex;

            // Transparent fallback
            var t = new Texture2D(38, 38, TextureFormat.ARGB32, false);
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    t.SetPixel(x, y, new Color(0, 0, 0, 0));
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return;

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, WindowTitle, HighLogic.Skin.window);
        }

        private void DrawWindow(int id)
        {
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) { GUI.DragWindow(); return; }

            var s = CareerLoansSettings.Get();

            GUILayout.BeginVertical();

            // Caps based on reputation (from settings)
            int maxLoansAllowed = 1;
            double maxPrincipalAllowed = 500_000.0;
            if (LoanScenario.Instance != null)
                LoanScenario.Instance.GetLoanCapsFromReputation(out maxLoansAllowed, out maxPrincipalAllowed);

            int currentLoans = (LoanScenario.Instance != null) ? LoanScenario.Instance.ActiveLoans.Count : 0;

            // Header info
            double funds = Funding.Instance != null ? Funding.Instance.Funds : 0.0;
            GUILayout.Label("Available Funds: " + funds.ToString("N0", CultureInfo.InvariantCulture));

            string repStr = (Reputation.Instance != null)
                ? Reputation.Instance.reputation.ToString("N0", CultureInfo.InvariantCulture)
                : "0";
            GUILayout.Label("Reputation: " + repStr);

            double apr = (LoanScenario.Instance != null) ? LoanScenario.Instance.ComputeAPRFromReputation() : s.APRMax;
            GUILayout.Label($"Current APR (based on rep): {(apr * 100.0):F2}%");

            GUILayout.Label($"Loans allowed: {currentLoans}/{maxLoansAllowed} used");
            GUILayout.Label("Max new loan amount: " + maxPrincipalAllowed.ToString("N0", CultureInfo.InvariantCulture));

            // Active loans list
            GUILayout.Space(8);
            GUILayout.Label("Active Loans:");

            double totalMonthly = 0.0;
            double totalRemaining = 0.0;

            // queue any loans to pay off (can't remove while iterating)
            var loansToPayoff = new List<Loan>();

            loansScroll = GUILayout.BeginScrollView(loansScroll, GUILayout.Height(210));
            if (LoanScenario.Instance != null && LoanScenario.Instance.ActiveLoans.Count > 0)
            {
                foreach (var l in LoanScenario.Instance.ActiveLoans)
                {
                    double remaining = LoanScenario.RemainingBalance(l);
                    int monthsLeft = Math.Max(0, l.TermMonths - l.PaymentsMade);
                    double nextInDays = Math.Max(0.0, (l.NextPaymentUT - Planetarium.GetUniversalTime()) / LoanScenario.SecondsPerKerbinDay);

                    totalMonthly += l.MonthlyPayment;
                    totalRemaining += remaining;

                    GUILayout.BeginVertical(HighLogic.Skin.textArea);
                    GUILayout.Label("ID: " + l.Id.ToString().Substring(0, 8)
                                    + "   APR " + (l.APR * 100.0).ToString("F2") + "%   "
                                    + l.PaymentsMade + "/" + l.TermMonths + " paid");
                    GUILayout.Label("Monthly: " + l.MonthlyPayment.ToString("N0", CultureInfo.InvariantCulture)
                                    + "   Remaining: " + remaining.ToString("N0", CultureInfo.InvariantCulture));
                    GUILayout.Label("Months left: " + monthsLeft
                                    + "   Next payment in ~" + nextInDays.ToString("0.#", CultureInfo.InvariantCulture) + " Kerbin days");

                    // --- Pay off now button ---
                    bool canPayOff = Funding.Instance != null && Funding.Instance.Funds >= remaining && remaining > 0.01;
                    GUI.enabled = canPayOff;
                    if (GUILayout.Button("Pay off now (" + remaining.ToString("N0", CultureInfo.InvariantCulture) + ")", GUILayout.Height(22)))
                    {
                        loansToPayoff.Add(l);
                    }
                    GUI.enabled = true;

                    GUILayout.EndVertical();
                }
            }
            else
            {
                GUILayout.Label("No active loans.");
            }
            GUILayout.EndScrollView();

            // process queued payoffs after listing
            if (loansToPayoff.Count > 0 && LoanScenario.Instance != null)
            {
                foreach (var l in loansToPayoff)
                    LoanScenario.Instance.PayOffLoan(l);
            }

            // Totals
            GUILayout.Label("Total monthly outgoing: " + totalMonthly.ToString("N0", CultureInfo.InvariantCulture));
            GUILayout.Label("Total remaining debt: " + totalRemaining.ToString("N0", CultureInfo.InvariantCulture));

            // New loan controls
            GUILayout.Space(8);
            GUILayout.Label("Loan Amount");
            string amtStr = GUILayout.TextField(loanAmount.ToString("N0", CultureInfo.InvariantCulture), GUILayout.Width(200));
            if (double.TryParse(amtStr, NumberStyles.AllowThousands | NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedAmt))
            {
                loanAmount = Math.Max(0, Math.Min(maxPrincipalAllowed, parsedAmt));
            }
            loanAmount = Mathf.Clamp((float)loanAmount, 0f, (float)maxPrincipalAllowed);
            loanAmount = GUILayout.HorizontalSlider((float)loanAmount, 0f, (float)maxPrincipalAllowed, GUILayout.Width(320));

            GUILayout.Space(6);
            GUILayout.Label("Term (months):");
            string termStr = GUILayout.TextField(termMonths.ToString(), GUILayout.Width(60));
            if (int.TryParse(termStr, out var parsedTerm))
                termMonths = Mathf.Clamp(parsedTerm, 1, s.MaxTermMonths);
            termMonths = Mathf.Clamp((int)GUILayout.HorizontalSlider(termMonths, 1, s.MaxTermMonths, GUILayout.Width(320)), 1, s.MaxTermMonths);
            GUILayout.Label("= " + (termMonths / 12f).ToString("0.##", CultureInfo.InvariantCulture) + " years (max " + (s.MaxTermMonths / 12f).ToString("0.##") + ")");

            double payment = LoanScenario.PaymentFor(loanAmount, apr, termMonths);
            GUILayout.Label("Estimated Monthly Payment: " + payment.ToString("N0", CultureInfo.InvariantCulture));

            bool canTakeMoreLoans = currentLoans < maxLoansAllowed;
            GUI.enabled = canTakeMoreLoans && loanAmount > 0.0 && termMonths >= 1;

            if (GUILayout.Button("Take Loan", GUILayout.Height(30)))
            {
                TakeLoan(loanAmount, apr, termMonths);
            }
            GUI.enabled = true;

            if (!canTakeMoreLoans)
            {
                GUILayout.Label("Loan limit reached for your current Reputation.", HighLogic.Skin.label);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Close", GUILayout.Height(24))) visible = false;

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void TakeLoan(double amount, double apr, int months)
        {
            if (LoanScenario.Instance == null) return;
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return;

            LoanScenario.Instance.GetLoanCapsFromReputation(out int maxLoansAllowed, out double maxPrincipalAllowed);
            int currentLoans = LoanScenario.Instance.ActiveLoans.Count;
            if (currentLoans >= maxLoansAllowed) return;

            amount = Math.Max(0.0, Math.Min(maxPrincipalAllowed, amount));
            var s = CareerLoansSettings.Get();
            months = Mathf.Clamp(months, 1, s.MaxTermMonths);

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                Principal = amount,
                APR = apr,
                TermMonths = months,
                MonthlyPayment = Math.Round(LoanScenario.PaymentFor(amount, apr, months)),
                PaymentsMade = 0,
                Remaining = amount,
                StartUT = Planetarium.GetUniversalTime(),
                NextPaymentUT = Planetarium.GetUniversalTime() + LoanScenario.SecondsPerMonth
            };

            LoanScenario.Instance.ActiveLoans.Add(loan);
            Funding.Instance.AddFunds(amount, TransactionReasons.ContractAdvance);
        }
    }
}
