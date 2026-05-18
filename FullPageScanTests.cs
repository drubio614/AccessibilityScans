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
            Console.WriteLine($"Axe JSON saved to: {jsonFile}");

            var htmlDir = Path.Combine(Directory.GetCurrentDirectory(), "a11y-html-reports");
            Directory.CreateDirectory(htmlDir);
            var htmlFile = Path.Combine(htmlDir, "beforepage_accessibility_report.html");
            File.WriteAllText(htmlFile, BuildHtmlReport(result, driver.Url));
            Console.WriteLine($"Axe HTML report saved to: {htmlFile}");

            if (GetLength(result, "Violations") > 0)
            {
                Assert.Fail($"Accessibility violations found: {GetLength(result, "Violations")}. See reports in {jsonDir} and {htmlDir}.");
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
            if (violations.Any())
            {
                sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
                sb.AppendLine("<tr><th>Rule</th><th>Impact</th><th>Description</th><th>Selector</th><th>HTML</th></tr>");

                foreach (var rule in violations)
                {
                    foreach (var node in ToEnumerable(rule, "Nodes"))
                    {
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

            return "(no