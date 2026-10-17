extern alias pawn;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PawnShopManagement.Data;
using PawnShopManagement.Models;

var root=Environment.GetEnvironmentVariable("PAWN_PROJECT_ROOT") ?? throw new Exception("Set PAWN_PROJECT_ROOT to the application directory.");
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_PAWNSHOPMANAGEMENT",root);
var name="pawn-checks-"+Guid.NewGuid();
var sqlConnection=Environment.GetEnvironmentVariable("PAWN_SQL_TEST_CONNECTION");
if(!string.IsNullOrEmpty(sqlConnection)) {
 var connection=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnection);
 connection.InitialCatalog="PawnShopVerification_"+Guid.NewGuid().ToString("N");
 sqlConnection=connection.ConnectionString;
 Console.WriteLine("SQL Server verification database: "+connection.InitialCatalog);
}
using var factory=new WebApplicationFactory<pawn::Program>().WithWebHostBuilder(b=> {
 b.UseContentRoot(root).UseEnvironment("Development");
 b.ConfigureTestServices(s=> {
   s.RemoveAll<ApplicationDbContext>(); s.RemoveAll<DbContextOptions<ApplicationDbContext>>();
   s.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
   s.AddDbContext<ApplicationDbContext>(o=> { if(sqlConnection==null)o.UseInMemoryDatabase(name);else o.UseSqlServer(sqlConnection); });
 });
});
if(Environment.GetEnvironmentVariable("PAWN_BROWSER_CHECK")=="1") factory.UseKestrel();
using var firstVisitClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
var firstVisit = await firstVisitClient.GetAsync("/Account/Start");
using(var scope=factory.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
 var hasher=scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
 foreach(var role in new[]{"Admin","Staff"}) {var u=new AppUser{UserName=role.ToLower(),Role=role};u.PasswordHash=hasher.HashPassword(u,"OnlyForTest123!");db.Add(u);}
 var customer=new Customer{FullName="Khách kiểm thử",IdentityCard="000000000001",Phone="0900000001",Address="Dữ liệu kiểm thử"}; db.Add(customer);db.SaveChanges();
 for(int i=0;i<12;i++) db.Add(new PawnContract{CustomerID=customer.CustomerID,AssetName="Tài sản kiểm thử "+i,LoanAmount=1000000,InterestRate=1.5f,Status=i<10?"DangCam":"DaChuoc",StartDate=DateTime.Today});
 db.SaveChanges();
}
if(Environment.GetEnvironmentVariable("PAWN_BROWSER_CHECK")=="1") {Console.WriteLine("Synthetic preview ready.");await Task.Delay(Timeout.Infinite);return;}
var messages=new List<string>();
void Check(bool ok,string label) { messages.Add((ok?"PASS ":"FAIL ")+label);Console.WriteLine(messages[^1]);if(!ok)throw new Exception(label); }
Check(firstVisit.StatusCode==HttpStatusCode.Redirect && firstVisit.Headers.Location?.OriginalString=="/Account/Setup","Fresh installation opens admin setup");
var nextVisit = await firstVisitClient.GetAsync("/Account/Start");
Check(nextVisit.StatusCode==HttpStatusCode.Redirect && nextVisit.Headers.Location?.OriginalString=="/Account/Login","Existing installation opens login");
Check(await firstVisitClient.GetStringAsync("/_launcher/ready")== (Environment.GetEnvironmentVariable("PAWN_LAUNCH_ID") ?? "ready"),"Launcher readiness endpoint identifies running instance");
Check(DatabaseConfiguration.ResolveConnectionString(null,true).Contains("(localdb)"),"Development missing connection uses LocalDB fallback");
Check(DatabaseConfiguration.ResolveConnectionString("  ",true).Contains("PawnShopDB"),"Development blank connection uses named demo database");
Check(DatabaseConfiguration.ResolveConnectionString("Server=custom;Database=Existing",false)=="Server=custom;Database=Existing","Explicit SQL connection is preserved");
try { DatabaseConfiguration.ResolveConnectionString(null,false); Check(false,"Production requires explicit SQL connection"); }
catch(InvalidOperationException){Check(true,"Production requires explicit SQL connection");}
HttpClient Client()=>factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false,BaseAddress=new Uri("https://localhost")});
async Task<string> Token(HttpClient client,string path="/Account/Login") {var html=await client.GetStringAsync(path);var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");if(!match.Success)throw new Exception("No CSRF token");return WebUtility.HtmlDecode(match.Groups[1].Value);}
async Task<HttpResponseMessage> Post(HttpClient c,string path,Dictionary<string,string> fields,string tokenPath="/Account/Login") {fields["__RequestVerificationToken"]=await Token(c,tokenPath);return await c.PostAsync(path,new FormUrlEncodedContent(fields));}
async Task Login(HttpClient c,string name) {var result=await Post(c,"/Account/Login",new(){["UserName"]=name,["Password"]="OnlyForTest123!"});Check(result.StatusCode==HttpStatusCode.Redirect,"Login "+name);}
using var anonymous=Client();
var english=await anonymous.GetStringAsync("/Account/Login?culture=en-US&ui-culture=en-US");
Check(english.Contains("Username")&&english.Contains("Password")&&english.Contains("Remember me"),"English sign-in labels localized");
Check((await anonymous.GetAsync("/Contracts")).StatusCode==HttpStatusCode.Redirect,"Anonymous MVC redirects to login");
Check((await anonymous.GetAsync("/api/contracts")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous API returns 401");
Check((await anonymous.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"CSRF rejects POST without token");
Check((await anonymous.GetAsync("/css/site.css")).StatusCode==HttpStatusCode.OK,"CSS served");
using var staff=Client();await Login(staff,"staff");
var englishContracts=await staff.GetStringAsync("/Contracts?culture=en-US&ui-culture=en-US");
Check(englishContracts.Contains("Outstanding principal")&&englishContracts.Contains("Loan principal")&&englishContracts.Contains("Apply filters"),"English contract list localized");
var englishForm=await staff.GetStringAsync("/Contracts/Create?culture=en-US&ui-culture=en-US");
Check(englishForm.Contains("Asset name")&&englishForm.Contains("Interest rate"),"English model display labels localized");
Check((await staff.GetAsync("/Account/CreateStaff")).StatusCode==HttpStatusCode.Redirect,"Staff cannot create staff accounts");
var list=await staff.GetStringAsync("/Contracts?statusFilter=DangCam&page=1");
Check(list.Contains("Tài sản kiểm thử")||list.Contains("T&#xE0;i"),"Contract view renders seeded records");
var json=await staff.GetFromJsonAsync<JsonElement>("/api/contracts?status=DangCam&page=999");
Check(json.GetProperty("total").GetInt32()==10&&json.GetProperty("page").GetInt32()==1&&json.GetProperty("items").GetArrayLength()==10,"API filters and clamps paging");
json=await staff.GetFromJsonAsync<JsonElement>("/api/contracts?page=2");Check(json.GetProperty("items").GetArrayLength()==2,"API second page contains two records");
var ajax=new HttpRequestMessage(HttpMethod.Get,"/Contracts?statusFilter=DaChuoc");ajax.Headers.Add("X-Requested-With","XMLHttpRequest");var partial=await (await staff.SendAsync(ajax)).Content.ReadAsStringAsync();Check(!partial.Contains("<html")&&partial.Contains("data-table"),"AJAX returns result fragment");
var newCustomer=await Post(staff,"/Customers/Create",new(){["FullName"]="Test Customer",["IdentityCard"]="000000000002",["Phone"]="0900000002",["Address"]="Test address"},"/Customers/Create");Check(newCustomer.StatusCode==HttpStatusCode.Redirect,"Create customer");
var duplicate=await Post(staff,"/Customers/Create",new(){["FullName"]="Duplicate",["IdentityCard"]="000000000002",["Phone"]="0900000002",["Address"]="Test"},"/Customers/Create");Check(duplicate.StatusCode==HttpStatusCode.OK,"Duplicate CCCD returns form");
var contractFields=new Dictionary<string,string>{["CustomerID"]="1",["AssetName"]="Decimal check",["LoanAmount"]="2500000",["InterestRate"]="1.5",["StartDate"]=DateTime.Today.ToString("yyyy-MM-dd"),["Status"]="DangCam"};
var created=await Post(staff,"/Contracts/Create",contractFields,"/Contracts/Create");Check(created.StatusCode==HttpStatusCode.Redirect,"Create contract under vi-VN");
int id;
using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var m=db.PawnContracts.Single(x=>x.AssetName=="Decimal check");id=m.ContractID;Check(m.InterestRate==1.5f&&m.LoanAmount==2500000,"Decimal point stored correctly");}
contractFields["ContractID"]=id.ToString();contractFields["Status"]="DaChuoc";
var edited=await Post(staff,"/Contracts/Edit/"+id,contractFields,"/Contracts/Edit/"+id);Check(edited.StatusCode==HttpStatusCode.Redirect,"Edit contract");
var token=await staff.GetFromJsonAsync<JsonElement>("/Account/Token");
var forbidden=new HttpRequestMessage(HttpMethod.Delete,"/api/contracts/"+id);forbidden.Headers.Add("RequestVerificationToken",token.GetProperty("requestToken").GetString());Check((await staff.SendAsync(forbidden)).StatusCode==HttpStatusCode.Forbidden,"Staff API delete forbidden");
var invalid=await Post(staff,"/Contracts/Create",new(){["CustomerID"]="999",["AssetName"]="bad",["LoanAmount"]="1",["InterestRate"]="-1",["StartDate"]="2100-01-01",["Status"]="Invalid"},"/Contracts/Create");Check(invalid.StatusCode==HttpStatusCode.OK,"Invalid contract returns validation form");
using(var scope=factory.Services.CreateScope())Check(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PawnContracts.Count()==13,"Invalid create did not persist");
using var admin=Client();await Login(admin,"admin");
token=await admin.GetFromJsonAsync<JsonElement>("/Account/Token");
async Task<HttpResponseMessage> Api(HttpMethod method,string path,object? body=null){var request=new HttpRequestMessage(method,path);request.Headers.Add("RequestVerificationToken",token.GetProperty("requestToken").GetString());if(body!=null)request.Content=JsonContent.Create(body);return await admin.SendAsync(request);}
var body=new {customerID=1,assetName="API test",loanAmount=3000000,interestRate=1.5,startDate=DateTime.Today,status="DangCam"};
var apiCreated=await Api(HttpMethod.Post,"/api/contracts",body);Check(apiCreated.StatusCode==HttpStatusCode.Created&&apiCreated.Headers.Location!=null,"REST POST returns 201 and Location");
var apiId=(await apiCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contractID").GetInt32();
Check((await Api(HttpMethod.Put,"/api/contracts/"+apiId,new{contractID=apiId,customerID=1,assetName="API edited",loanAmount=3000000,interestRate=2.5,startDate=DateTime.Today,status="DaChuoc"})).StatusCode==HttpStatusCode.NoContent,"REST PUT returns 204");
Check((await Api(HttpMethod.Delete,"/api/contracts/"+apiId)).StatusCode==HttpStatusCode.NoContent,"REST DELETE returns 204");
Check((await admin.GetAsync("/api/contracts/"+apiId)).StatusCode==HttpStatusCode.NotFound,"Deleted contract returns 404");
var delCustomer=await Post(admin,"/Customers/Delete/1",new(),"/Customers");Check(delCustomer.StatusCode==HttpStatusCode.Redirect,"Delete referenced customer redirects safely");
using(var scope=factory.Services.CreateScope())Check(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Customers.Any(x=>x.CustomerID==1),"Referenced customer remains intact");
var lang=await Post(staff,"/Home/Language",new(){["culture"]="en-US",["returnUrl"]="https://example.com"});Check(lang.Headers.Location?.OriginalString=="/","Language endpoint prevents external redirect");
await Post(staff,"/Account/Logout",new());Check((await staff.GetAsync("/api/contracts")).StatusCode==HttpStatusCode.Unauthorized,"Logout clears authentication");
for(int attempt=0;attempt<5;attempt++) {using var attacker=Client();await Post(attacker,"/Account/Login",new(){["UserName"]="staff",["Password"]="WrongPassword123"});}
using(var scope=factory.Services.CreateScope())Check(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AppUsers.Single(x=>x.UserName=="staff").LockoutUntil>DateTime.UtcNow,"Account lockout persists across fresh sessions");
using var lockedClient=Client();var lockedLogin=await Post(lockedClient,"/Account/Login",new(){["UserName"]="staff",["Password"]="OnlyForTest123!"});Check(lockedLogin.StatusCode==HttpStatusCode.OK&&(await lockedClient.GetAsync("/api/contracts")).StatusCode==HttpStatusCode.Unauthorized,"Locked account cannot authenticate with correct password");
if(sqlConnection!=null) {
 using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
 async Task Reject(string sql,string label){await using var tx=await db.Database.BeginTransactionAsync();try{await db.Database.ExecuteSqlRawAsync(sql);Check(false,label);}catch(Microsoft.Data.SqlClient.SqlException){Check(true,label);}finally{await tx.RollbackAsync();}}
 await Reject("INSERT INTO Customers(FullName,IdentityCard,Phone,Address) VALUES(N'Test',N'000000000001',N'0900000001',N'Test')","SQL unique identity constraint");
 await Reject("INSERT INTO PawnContracts(CustomerID,AssetName,LoanAmount,InterestRate,StartDate,Status) VALUES(999999,N'Test',1000000,1,GETDATE(),N'DangCam')","SQL foreign key constraint");
 await Reject("INSERT INTO PawnContracts(CustomerID,AssetName,LoanAmount,InterestRate,StartDate,Status) VALUES(1,N'Test',1,1,GETDATE(),N'DangCam')","SQL loan CHECK constraint");
}
var deadlineToday=ContractDeadlines.Today;
var calendar=new PawnContract{StartDate=new DateTime(2024,1,31),TermDays=30};
Check(calendar.DueDate==new DateTime(2024,3,1),"Pawn term adds calendar days across leap February");
calendar.StartDate=new DateTime(2026,12,31);calendar.TermDays=1;
Check(calendar.DueDate==new DateTime(2027,1,1),"Pawn due date crosses year boundary");
var fixture=new[] {
 new PawnContract{AssetName="Deadline overdue",TermDays=9},new PawnContract{AssetName="Deadline today",TermDays=10},
 new PawnContract{AssetName="Deadline tomorrow",TermDays=11},new PawnContract{AssetName="Deadline three",TermDays=13},
 new PawnContract{AssetName="Deadline four",TermDays=14},new PawnContract{AssetName="Deadline redeemed",TermDays=1,Status="DaChuoc"},
 new PawnContract{AssetName="Deadline liquidated",TermDays=1,Status="DaThanhLy"},new PawnContract{AssetName="Deadline legacy",TermDays=null}
};
var states=new[]{"Overdue","DueToday","DueSoon","DueSoon","OnTime","Closed","Closed","Unknown"};
using(var scope=factory.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
 for(int i=0;i<fixture.Length;i++){var m=fixture[i];m.CustomerID=1;m.StartDate=deadlineToday.AddDays(-10);m.LoanAmount=1000000;db.Add(m);Check(m.GetDeadlineState(deadlineToday)==states[i],"Deadline state: "+m.AssetName);}
 await db.SaveChangesAsync();
}
foreach(var (filter,expected) in new[]{("Overdue",1),("DueToday",1),("DueSoon",2)}) {
 var result=await admin.GetFromJsonAsync<JsonElement>("/api/contracts?deadline="+filter);
 Check(result.GetProperty("total").GetInt32()==expected,"API deadline filter "+filter);
}
var alerts=new HttpRequestMessage(HttpMethod.Get,"/Contracts?deadlineFilter=DueSoon");alerts.Headers.Add("X-Requested-With","XMLHttpRequest");
var alertsHtml=await(await admin.SendAsync(alerts)).Content.ReadAsStringAsync();
Check(alertsHtml.Contains("deadline-overview")&&alertsHtml.Contains("deadlineFilter=DueSoon")&&!alertsHtml.Contains("<html"),"AJAX includes alerts and preserves deadline filter in paging links");
var termFields=new Dictionary<string,string>{["CustomerID"]="1",["AssetName"]="Term persistence",["LoanAmount"]="2500000",["InterestRate"]="1.5",["StartDate"]=deadlineToday.ToString("yyyy-MM-dd"),["Status"]="DangCam",["TermDays"]="45"};
Check((await Post(admin,"/Contracts/Create",termFields,"/Contracts/Create")).StatusCode==HttpStatusCode.Redirect,"MVC accepts explicit 45-day pawn term");
int termId;
using(var scope=factory.Services.CreateScope()){var m=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PawnContracts.Single(x=>x.AssetName=="Term persistence");termId=m.ContractID;Check(m.TermDays==45&&m.DueDate==deadlineToday.AddDays(45),"MVC persists pawn term and calculates due date");}
termFields["ContractID"]=termId.ToString();termFields["TermDays"]="60";
Check((await Post(admin,"/Contracts/Edit/"+termId,termFields,"/Contracts/Edit/"+termId)).StatusCode==HttpStatusCode.Redirect,"MVC edits pawn term");
var termJson=await admin.GetFromJsonAsync<JsonElement>("/api/contracts/"+termId);
Check(termJson.GetProperty("termDays").GetInt32()==60&&termJson.GetProperty("dueDate").GetDateTime()==deadlineToday.AddDays(60),"API exposes updated pawn term and due date");
foreach(var invalidTerm in new[]{"0","3651",""}) {
 termFields["AssetName"]="Invalid term "+invalidTerm;termFields["TermDays"]=invalidTerm;
 Check((await Post(admin,"/Contracts/Create",termFields,"/Contracts/Create")).StatusCode==HttpStatusCode.OK,"MVC rejects invalid pawn term: "+invalidTerm);
}
using(var scope=factory.Services.CreateScope())Check(!scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PawnContracts.Any(x=>x.AssetName.StartsWith("Invalid term")),"Invalid pawn terms do not persist");
Check((await Api(HttpMethod.Post,"/api/contracts",new{customerID=1,assetName="Invalid API term",loanAmount=1000000,interestRate=1,startDate=deadlineToday,termDays=0,status="DangCam"})).StatusCode==HttpStatusCode.BadRequest,"API rejects zero-day pawn term");
using(var scope=factory.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
 for(int i=0;i<9;i++)db.Add(new PawnContract{CustomerID=1,AssetName="Deadline pagination "+i,StartDate=deadlineToday.AddDays(-10),TermDays=11,LoanAmount=1000000});
 await db.SaveChangesAsync();
}
var deadlinePage=await admin.GetStringAsync("/Contracts?deadlineFilter=DueSoon");
Check(Regex.IsMatch(deadlinePage,"href=\"[^\"]*deadlineFilter=DueSoon[^\"]*page=2")||Regex.IsMatch(deadlinePage,"href=\"[^\"]*page=2[^\"]*deadlineFilter=DueSoon"),"Deadline filter preserved on actual next-page link");
var deadlinePageJson=await admin.GetFromJsonAsync<JsonElement>("/api/contracts?deadline=DueSoon&page=2");
Check(deadlinePageJson.GetProperty("total").GetInt32()==11&&deadlinePageJson.GetProperty("items").GetArrayLength()==1,"Deadline filter works on page two");
using(var translationDb=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=True").Options)) {
 foreach(var filter in new[]{"Overdue","DueToday","DueSoon"})Check(ContractDeadlines.Filter(translationDb.PawnContracts,filter,deadlineToday).ToQueryString().Contains("DATEADD"),"SQL Server translates deadline filter: "+filter);
}
var output=Environment.GetEnvironmentVariable("PAWN_CHECK_OUTPUT");if(output!=null){Directory.CreateDirectory(output);await File.WriteAllLinesAsync(Path.Combine(output,"integration-results.txt"),messages);}
Console.WriteLine($"All {messages.Count} checks passed. Provider: {(sqlConnection==null?"InMemory (SQL constraints not tested)":"SQL Server, isolated verification database retained")}");

