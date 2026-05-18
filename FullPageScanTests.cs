using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Deque.AxeCore.Selenium;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace AccessibilityScans
{
    [TestFixture]
    public class FullPageScanTests
    {
        private IWebDriver? driver;

        [SetUp]
        public void SetUp()
        {
            var options = new FirefoxOptions();
            options.AddArgument("--width=1920");
            options.AddArgument("--height=1080");
            options.AddArgument("--headless"); // REQUIRED FOR GITHUB ACTIONS

            driver = new FirefoxDriver(options);
        }

        [Test]
        public void BeforePage_Should_Have_No_Accessibility_Violations()
        {
            Console.WriteLine("Working Directory: " + Directory.GetCurrentDirectory());

            driver!.Navigate().GoToUrl("https://projects.accesscomputing.uw.edu/au/before.html");

            dynamic result = new AxeBuilder(driver)
                .WithTags("wcag2a", "wcag2aa", "wcag21aa")
                .Analyze();

            Console.WriteLine($"Violations: {GetLength(result, "Violations")}");
            Console.WriteLine($"Passes: {GetLength(result, "Passes")}");
            Console.WriteLine($"Incomplete: {GetLength(result, "Incomplete")}");
            Console.WriteLine($"Inapplicable: {GetLength(result, "Inapplicable")}");

            foreach (var v in ToEnumerable(result, "Violations"))
            {
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Rule: {GetPropString(v, "Id")}");
                Console.WriteLine($"Impact: {GetPropString(v, "Impact")}");
                Console.WriteLine($"Description: {GetPropString(v, "Description")}");
                Console.WriteLine($"Help: {GetPropString(v, "Help")} | {GetPropString(v, "HelpUrl")}");
                var nodes = ToEnumerable(v, "Nodes");
                Console.WriteLine($"Nodes: {GetLength(v, "Nodes")}");

                foreach (var node in nodes)
                {
                    var selector = GetSelector(node);
                    Console.WriteLine($"  Selector: {selector}");
                    Console.WriteLine($"  HTML: {Truncate(GetPropString(node, "Html"), 1000)}");
                    Console.WriteLine($"  FailureSummary: {Truncate(GetFailureSummary(node), 1000)}");
                }
            }

            var jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "a11y-json-reports");
            Directory.CreateDirectory(jsonDir);
            var jsonFile = Path.Combine(jsonDir, "beforepage_axe_results.json");
            File.WriteAllText(jsonFile, JsonConvert.SerializeObject(result, Formatting.Indented));

            var htmlDir = Path.Combine(Directory.GetCurrentDirectory(), "a11y-html-reports");
            Directory.CreateDirectory(htmlDir);
            var htmlFile = Path.Combine(htmlDir, "beforepage_accessibility_report.html");
            File.WriteAllText(htmlFile, BuildHtmlReport(result, driver.Url));

            if (GetLength(result, "Violations") > 0)
            {
                Console.WriteLine("⚠ Accessibility violations detected. See JSON/HTML reports for details.");
                Assert.Warn($"Accessibility violations found: {GetLength(result, "Violations")}. See reports in {jsonDir} and {htmlDir}.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            driver?.Quit();
            driver?.Dispose();
        }

        private static string BuildHtmlReport(dynamic result, string url)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8'><title>A11y Report</title></head><body>");
            sb.AppendLine($"<h1>Accessibility Report for {System.Net.WebUtility.HtmlEncode(url)}</h1>");
            sb.AppendLine($"<p>Total Violations: {GetLength(result, "Violations")}</p>");

            var violations = ToEnumerable(result, "Violations");
            bool hasRows = false;

            foreach (var rule in violations)
            {
                foreach (var node in ToEnumerable(rule, "Nodes"))
                {
                    if (!hasRows)
                    {
                        sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
                        sb.AppendLine("<tr><th>Rule</th><th>Impact</th><th>Description</th><th>Selector</th><th>HTML</th></tr>");
                        hasRows = true;
                    }

                    var selector = GetSelector(node);
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(GetPropString(rule, "Id"))}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(GetPropString(rule, "Impact"))}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(GetPropString(rule, "Description"))}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(selector)}</td>");
                    sb.AppendLine($"<td><pre style='white-space:pre-wrap'>{System.Net.WebUtility.HtmlEncode(GetPropString(node, "Html"))}</pre></td>");
                    sb.AppendLine("</tr>");
                }
            }

            if (hasRows)
            {
                sb.AppendLine("</table>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string GetPropString(object? obj, string propName)
        {
            if (obj == null) return "";
            var t = obj.GetType();
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
            {
                var val = p.GetValue(obj);
                return val?.ToString() ?? "";
            }

            var f = t.GetField(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f != null)
            {
                var val = f.GetValue(obj);
                return val?.ToString() ?? "";
            }

            return obj.ToString();
        }

        private static string GetSelector(object? node)
        {
            if (node == null) return "(no selector)";
            var t = node.GetType();

            var targetProp = t.GetProperty("Target", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (targetProp != null)
            {
                var targetVal = targetProp.GetValue(node);
                if (targetVal == null) return "(no selector)";

                if (targetVal is System.Collections.IEnumerable enumerable && targetVal is not string)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var item in enumerable)
                    {
                        if (item != null) list.Add(item.ToString()!);
                    }
                    if (list.Count > 0) return string.Join(", ", list);
                }

                return targetVal.ToString() ?? "(no selector)";
            }

            var selectorProp = t.GetProperty("Selector", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (selectorProp != null)
            {
                var val = selectorProp.GetValue(node);
                return val?.ToString() ?? "(no selector)";
            }

            return "(no selector)";
        }

        private static string GetFailureSummary(object? node)
        {
            if (node == null) return "";
            var t = node.GetType();

            string[] names = { "FailureSummary", "failureSummary", "FailureSummaryText", "Summary", "failure_summary" };
            foreach (var name in names)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var val = p.GetValue(node);
                    return val?.ToString() ?? "";
                }
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (f != null)
                {
                    var val = f.GetValue(node);
                    return val?.ToString() ?? "";
                }
            }

            var html = GetPropString(node, "Html");
            var any = GetPropString(node, "Any");
            if (!string.IsNullOrEmpty(any)) return any;
            return !string.IsNullOrEmpty(html) ? Truncate(html, 200) : "";
        }

        private static int GetLength(object? obj, string propName)
        {
            if (obj == null) return 0;
            var t = obj.GetType();
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
            {
                var val = p.GetValue(obj);
                if (val is System.Collections.ICollection coll) return coll.Count;
                if (val is System.Collections.IEnumerable en)
                {
                    int c = 0;
                    foreach (var _ in en) c++;
                    return c;
                }
            }
            return 0;
        }

        private static System.Collections.Generic.IEnumerable<object> ToEnumerable(object? obj, string propName)
        {
            if (obj == null) return Enumerable.Empty<object>();
            var t = obj.GetType();
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            object? val = null;

            if (p != null)
            {
                val = p.GetValue(obj);
            }
            else if (obj is System.Collections.IEnumerable en && obj is not string)
            {
                return en.Cast<object>();
            }

            if (val == null) return Enumerable.Empty<object>();
            if (val is System.Collections.IEnumerable en2 && val is not string)
            {
                return en2.Cast<object>();
            }

            return new[] { val };
        }

        private static string Truncate(string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return input ?? "";
            return input.Length <= maxLength ? input : input.Substring(0, maxLength) + "...(truncated)";
        }
    }
}
