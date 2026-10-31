"""Build an evidence page from real TRX/JSON outputs; never invent passing results."""
from pathlib import Path
import html,json,sys,xml.etree.ElementTree as ET
root=Path(sys.argv[1] if len(sys.argv)>1 else 'TestResults/Assignment')
root.mkdir(parents=True,exist_ok=True)
def h(x):return html.escape(str(x))
def results(filename):
 rows=[]
 for p in root.rglob(filename):
  for item in ET.parse(p).getroot().iter():
   if item.tag.endswith('UnitTestResult'):rows.append((item.get('testName',''),item.get('outcome',''),item.get('duration','')))
 return rows
def table(rows):
 if not rows:return '<p class="missing">No result file was produced. This section is unverified.</p>'
 return '<table><thead><tr><th>Test</th><th>Outcome</th><th>Duration</th></tr></thead><tbody>'+''.join(f'<tr><td>{h(n)}</td><td class="{("pass" if o=="Passed" else "fail")}">{h(o)}</td><td>{h(d)}</td></tr>' for n,o,d in rows)+'</tbody></table>'
def load(name):
 p=root/name
 return json.loads(p.read_text()) if p.exists() else None
unit=results('assignment-unit.trx');system=results('assignment-system.trx')
integration=load('integration-result.json');performance=load('load-stress-result.json');environment=load('environment.json')
sections=[]
sections.append('<section id="unit-results"><span class="label">A.1 · Actual .NET runner results</span><h2>Unit tests</h2><p>Visual Studio-compatible xUnit tests. This is a CI evidence report, not a Visual Studio screenshot.</p>'+table(unit)+'</section>')
integrows=[r for r in system if 'DonationFlowIntegrationTests' in r[0]]
body=table(integrows)
if integration:body+='<h3>Verified component flow</h3><p>'+h(integration['architecture'])+'</p><pre>'+h(json.dumps(integration,indent=2))+'</pre>'
sections.append('<section id="integration-results"><span class="label">A.2 · Real web / Functions / SQL integration</span><h2>Donation flow</h2>'+body+'</section>')
body=table([r for r in system if 'LoadAndStressTests' in r[0]])
if performance:
 body+='<p>'+h(performance['environment'])+'</p><table><thead><tr><th>Phase</th><th>Virtual users</th><th>Peak requests</th><th>Requests</th><th>Success</th><th>Mean ms</th><th>P95 ms</th><th>Requests/s</th></tr></thead><tbody>'
 for r in performance['results']:
  body+=f'<tr><td>{h(r["Name"])}</td><td>{r["VirtualUsers"]}</td><td>{r["PeakConcurrentRequests"]}</td><td>{r["Requests"]}</td><td>{r["SuccessRate"]*100:.2f}%</td><td>{r["MeanMilliseconds"]:.2f}</td><td>{r["P95Milliseconds"]:.2f}</td><td>{r["RequestsPerSecond"]:.2f}</td></tr>'
 body+='</tbody></table><h3>Executed configuration</h3><pre>'+h(json.dumps(performance['settings'],indent=2))+'</pre><p>Closed-loop workload with think time; a small local stress stage does not establish maximum production capacity.</p>'
sections.append('<section id="performance-results"><span class="label">A.3 · Measured load and stress results</span><h2>Five-user load and twenty-user stress</h2>'+body+'</section>')
body=table([r for r in system if 'SeleniumUiTests' in r[0]])
body+='<p>Actual Chrome WebDriver screenshots are saved separately: employee login/dashboard, anonymous form/review/confirmation. Password fields are masked by the browser.</p>'
sections.append('<section id="selenium-results"><span class="label">A.4 · Actual Selenium WebDriver runs</span><h2>Browser automation</h2>'+body+'</section>')
allrows=unit+system
passed=sum(1 for r in allrows if r[1]=='Passed')
failed=sum(1 for r in allrows if r[1]=='Failed')
header=f'<header><span class="label">Gift of the Givers · Assignment evidence</span><h1>Testing results</h1><p>{passed} passed · {failed} failed · {len(allrows)} recorded results</p><p>Generated from actual TRX and measured JSON outputs. Visual Studio Test Explorer screenshots must be captured locally.</p></header>'
css='''body{margin:0;background:#f3f6f5;color:#15382e;font-family:Arial,sans-serif}main{max-width:1200px;margin:auto;padding:28px}header,section{background:white;border:1px solid #dce6df;border-radius:14px;padding:30px;margin-bottom:24px}header{background:#0a4438;color:white}h1{font-size:36px}.label{text-transform:uppercase;letter-spacing:.08em;font-size:12px;font-weight:bold}table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;vertical-align:top;padding:12px;border-bottom:1px solid #dce6df;overflow-wrap:anywhere}th{background:#edf5ef}.pass{color:#0c7354;font-weight:bold}.fail,.missing{color:#b42318;font-weight:bold}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f3f6f5;padding:16px;border-radius:8px;font-size:12px}p{line-height:1.6}'''
(root/'evidence-index.html').write_text('<!doctype html><html lang="en"><meta charset="utf-8"><title>Measured assignment test evidence</title><style>'+css+'</style><main>'+header+''.join(sections)+'</main></html>')
(root/'evidence-summary.json').write_text(json.dumps({'recorded':len(allrows),'passed':passed,'failed':failed,'environment':environment,'note':'Actual CI results, not Visual Studio screenshots.'},indent=2))
print(f'Evidence report: {len(allrows)} recorded results, {passed} passed, {failed} failed')
