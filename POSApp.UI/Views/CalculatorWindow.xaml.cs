using System.Windows;
using System.Windows.Controls;

namespace POSApp.UI.Views
{
    public partial class CalculatorWindow : Window
    {
        private static CalculatorWindow? _instance;

        private string _currentInput = "0";
        private double _firstOperand = 0;
        private string _operator = string.Empty;
        private bool _newInput = true;
        private string _expression = string.Empty;

        public static void ShowCalculator()
        {
            if (_instance == null || !_instance.IsVisible)
            {
                _instance = new CalculatorWindow();
                // Position near right side of screen
                _instance.Left = SystemParameters.WorkArea.Right - _instance.Width - 20;
                _instance.Top = SystemParameters.WorkArea.Top + 100;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        public CalculatorWindow()
        {
            InitializeComponent();
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            var tag = (string)((Button)sender).Tag;

            switch (tag)
            {
                case "0": case "1": case "2": case "3": case "4":
                case "5": case "6": case "7": case "8": case "9":
                    AppendDigit(tag);
                    break;
                case ".":
                    AppendDecimal();
                    break;
                case "+": case "-": case "*": case "/":
                    SetOperator(tag);
                    break;
                case "=":
                    Calculate();
                    break;
                case "C":
                    Clear();
                    break;
                case "BS":
                    Backspace();
                    break;
                case "%":
                    ApplyPercent();
                    break;
                case "NEG":
                    Negate();
                    break;
            }

            MainDisplay.Text = _currentInput;
            ExpressionDisplay.Text = _expression;
        }

        private void AppendDigit(string digit)
        {
            if (_newInput)
            {
                _currentInput = digit;
                _newInput = false;
            }
            else
            {
                _currentInput = _currentInput == "0" ? digit : _currentInput + digit;
            }
        }

        private void AppendDecimal()
        {
            if (_newInput) { _currentInput = "0"; _newInput = false; }
            if (!_currentInput.Contains('.'))
                _currentInput += ".";
        }

        private void SetOperator(string op)
        {
            if (!_newInput && !string.IsNullOrEmpty(_operator))
                Calculate();

            if (double.TryParse(_currentInput, out double val))
                _firstOperand = val;

            _operator = op;
            _expression = $"{_currentInput} {op}";
            _newInput = true;
        }

        private void Calculate()
        {
            if (string.IsNullOrEmpty(_operator)) return;
            if (!double.TryParse(_currentInput, out double second)) return;

            double result = _operator switch
            {
                "+" => _firstOperand + second,
                "-" => _firstOperand - second,
                "*" => _firstOperand * second,
                "/" => second != 0 ? _firstOperand / second : double.NaN,
                _ => second
            };

            _expression = $"{_firstOperand} {_operator} {second} =";
            _currentInput = double.IsNaN(result) ? "Error" : FormatResult(result);
            _operator = string.Empty;
            _firstOperand = result;
            _newInput = true;
        }

        private void Clear()
        {
            _currentInput = "0";
            _firstOperand = 0;
            _operator = string.Empty;
            _expression = string.Empty;
            _newInput = true;
        }

        private void Backspace()
        {
            if (_newInput || _currentInput.Length <= 1)
            {
                _currentInput = "0";
                _newInput = false;
            }
            else
            {
                _currentInput = _currentInput[..^1];
                if (_currentInput == "-") _currentInput = "0";
            }
        }

        private void ApplyPercent()
        {
            if (double.TryParse(_currentInput, out double val))
                _currentInput = FormatResult(val / 100);
        }

        private void Negate()
        {
            if (_currentInput != "0")
                _currentInput = _currentInput.StartsWith("-") ? _currentInput[1..] : "-" + _currentInput;
        }

        private static string FormatResult(double value)
        {
            if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
                return ((long)value).ToString();
            return value.ToString("G10").TrimEnd('0').TrimEnd('.');
        }
    }
}
