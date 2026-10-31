using GiftOfTheGivers.AssignmentTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;
namespace GiftOfTheGivers.AssignmentTests.Selenium;

[Collection("Assignment system"), Trait("Category", "Selenium")]
public class SeleniumUiTests(AssignmentEnvironment environment, ITestOutputHelper output)
{
    [Fact]
    public void EmployeeCanLoginAndReachTheStaffDashboard()
    {
        using var browser = Browser();
        try
        {
            browser.Navigate().GoToUrl(environment.WebUrl + "/Identity/Account/Login");
            browser.FindElement(By.Id("Input_Email")).SendKeys(AssignmentEnvironment.EmployeeEmail);
            browser.FindElement(By.Id("Input_Password")).SendKeys(AssignmentEnvironment.EmployeePassword);
            Capture(browser, "A4-employee-login-form.png");
            Click(browser, browser.FindElement(By.CssSelector("#account button[type='submit']")));
            Wait(browser).Until(driver => new Uri(driver.Url).AbsolutePath.StartsWith("/Staff", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Staff", browser.FindElement(By.CssSelector(".dash-role-badge")).Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Volunteer applications", browser.FindElement(By.CssSelector(".dash-sidebar")).Text, StringComparison.OrdinalIgnoreCase);
            Capture(browser, "A4-employee-dashboard-passed.png");
            output.WriteLine("PASS: employee signed in with a real Identity cookie and reached the Staff dashboard.");
        }
        catch { Capture(browser, "A4-employee-failure.png"); throw; }
    }

    [Fact]
    public async Task AnonymousDonorCanReviewAndConfirmAContribution()
    {
        using var browser = Browser();
        var email = "selenium-" + Guid.NewGuid().ToString("N")[..8] + "@example.test";
        try
        {
            browser.Navigate().GoToUrl(environment.WebUrl + "/Donations/Create");
            browser.FindElement(By.Id("DonorName")).SendKeys("Selenium Anonymous Donor");
            browser.FindElement(By.Id("Email")).SendKeys(email);
            var amount = browser.FindElement(By.Id("Amount")); amount.Clear(); amount.SendKeys("250.50");
            new SelectElement(browser.FindElement(By.Id("Currency"))).SelectByText("ZAR");
            new SelectElement(browser.FindElement(By.Id("ReliefProjectId"))).SelectByValue(environment.ProjectId.ToString());
            Capture(browser, "A4-anonymous-donation-form.png");
            Click(browser, browser.FindElement(By.CssSelector("form[action*='Review'] button[type='submit']")));
            Wait(browser).Until(driver => driver.FindElements(By.CssSelector("input[name='reviewToken']")).Count == 1);
            Assert.Contains("250.50", browser.FindElement(By.CssSelector(".review-amount")).Text);
            Capture(browser, "A4-anonymous-donation-review.png");
            Click(browser, browser.FindElement(By.CssSelector("form input[name='reviewToken']")).FindElement(By.XPath("..")).FindElement(By.CssSelector("button[type='submit']")));
            Wait(browser).Until(driver => new Uri(driver.Url).AbsolutePath.Contains("/Donations/Certificate/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Selenium Anonymous Donor", browser.FindElement(By.TagName("h1")).Text);
            Assert.Contains("250.50", browser.FindElement(By.CssSelector(".receipt-card")).Text);
            Assert.True(browser.FindElement(By.CssSelector("a[href*='/Donations/Download/']")).Displayed);
            Capture(browser, "A4-anonymous-donation-confirmation-passed.png");
            await using var db = environment.Database();
            var stored = await db.Donations.SingleAsync(d => d.Email == email);
            Assert.Null(stored.DonorId); Assert.Equal(250.50m, stored.Amount);
            output.WriteLine("PASS: anonymous donor completed Details -> Review -> Confirmation. SQL record {0} verified.", stored.Id);
        }
        catch { Capture(browser, "A4-anonymous-donation-failure.png"); throw; }
    }

    private ChromeDriver Browser()
    {
        var options = new ChromeOptions();
        if (!string.Equals(Environment.GetEnvironmentVariable("GOTG_SELENIUM_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase)) options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1440,1000"); options.AddArgument("--disable-dev-shm-usage");
        if (OperatingSystem.IsLinux()) options.AddArgument("--no-sandbox");
        var binary = Environment.GetEnvironmentVariable("GOTG_CHROME_BINARY"); if (!string.IsNullOrWhiteSpace(binary)) options.BinaryLocation = binary;
        var driverPath = Environment.GetEnvironmentVariable("GOTG_CHROMEDRIVER");
        var service = string.IsNullOrWhiteSpace(driverPath) ? ChromeDriverService.CreateDefaultService() : ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(driverPath), Path.GetFileName(driverPath));
        service.HideCommandPromptWindow = true;
        var browser = new ChromeDriver(service, options, TimeSpan.FromSeconds(90));
        browser.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(45);
        return browser;
    }
    private static void Click(ChromeDriver browser, IWebElement element)
    {
        // Site CSS uses smooth scrolling. Complete the scroll before WebDriver's real click.
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'nearest',behavior:'instant'});", element);
        Wait(browser).Until(_ => element.Displayed && element.Enabled && (bool)browser.ExecuteScript(
            "const e=arguments[0],r=e.getBoundingClientRect();const hit=document.elementFromPoint(r.x+r.width/2,r.y+r.height/2);return hit===e||e.contains(hit);", element));
        element.Click();
    }
    private static WebDriverWait Wait(IWebDriver browser) => new(browser, TimeSpan.FromSeconds(30));
    private void Capture(ITakesScreenshot browser, string name) => browser.GetScreenshot().SaveAsFile(Path.Combine(environment.EvidenceDirectory, name));
}
