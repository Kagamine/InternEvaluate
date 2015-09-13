**本例程将使用如下工具或框架：**

ASP.Net 5 Preview Beta 7

SQLite 3

MVC 6

Identity 3

<hr />

首先，创建一个ASP.Net 5 Preview Beta 7的空项目，命名为GuestBook。

创建完毕后，同样的需要向NuGet.config中添加vNext私有源：

```
<add key="vNext" value="https://www.myget.org/F/aspnetvnext/api/v2" />
```

配置project.json：

```
  "dependencies": {
    "Microsoft.AspNet.Server.IIS": "1.0.0-beta7",
    "Microsoft.AspNet.Server.WebListener": "1.0.0-beta7",
    "EntityFramework.Sqlite": "7.0.0-beta7",
    "Microsoft.AspNet.Identity.EntityFramework": "3.0.0-beta7",
    "Microsoft.Data.Sqlite": "1.0.0-beta8-15549",
    "Microsoft.AspNet.Mvc": "6.0.0-beta7",
    "Microsoft.AspNet.Mvc.TagHelpers": "6.0.0-beta7",
    "Microsoft.AspNet.StaticFiles": "1.0.0-beta7",
    "CodeComb.vNext": "1.0.37"
  }
```

经分析，我们需要实现的功能包括：(1)系统包含了三种角色：学生、组长、系主任 (2)允许系主任设置组长、为组内添加成员 (3)组长可为每个组员打分，并提供实名评价 (4) 组内学生可互相评分，并匿名评价 (5) 仅系主任可查看评分结果与学生的匿名、组长的实名评价 (6)可随时修改评价内容

数据库结构图如下：

![Upload](http://doc.codecomb.com/file/download/195261ba-6d26-437a-b00c-ba48e874617c)

首先创建User.cs模型，由于我们使用的Identity 3（Microsoft.AspNet.Identity）框架中包含了权限角色控制，因此我们不需要在User中设置Role字段：

```
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public class User : IdentityUser
    {
        // 班级
        [MaxLength(16)]
        public string Class { get; set; }

        // 姓名
        [MaxLength(8)]
        public string Name { get; set; }

        // 组
        [MaxLength(8)]
        public string Group { get; set; }

        // 学号
        [MaxLength(10)]
        public string StudentNumber { get; set; }

        // 获得的评价集合
        public virtual ICollection<Comment> Evaluated { get; set; } = new List<Comment>();

        // 给他人的评价集合
        public virtual ICollection<Comment> Evaluate { get; set; } = new List<Comment>();
    }
}
```

接下来创建Comment.cs模型：

```
using System.ComponentModel.DataAnnotations.Schema;

namespace InternEvaluate.Models
{
    public enum Level
    {
        不及格,
        及格,
        中等,
        良好,
        优秀
    }

    public class Comment
    {
        public int Id { get; set; }

        // 评价者用户ID
        [ForeignKey("Evaluator")]
        public string EvaluatorId { get; set; }

        public virtual User Evaluator { get; set; }

        // 被评价者用户ID
        [ForeignKey("Evaluated")]
        public string EvaluatedId { get; set; }

        public virtual User Evaluated { get; set; }

        // 评价内容
        public string Content { get; set; }

        // 评定级别
        public Level Level { get; set; }

        // 是否为组长评价
        public bool IsChargeman { get; set; }
    }
}
```

接下来创建数据库上下文类，因为我们使用了Identity框架，因此需要继承IdentityDbContext&lt;TUser&gt;，同时需要为Comment.Level添加索引：

```
using Microsoft.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public class EvaluateContext : IdentityDbContext<User>
    {
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Comment>(e => 
            {
                // 为评定等级添加索引
                e.Index(x => x.Level);

                // 为组长评价标记添加索引
                e.Index(x => x.IsChargeman);
            });

            builder.Entity<User>(e => 
            {
                // 为班级、学号和组别添加索引
                e.Index(x => x.Class);
                e.Index(x => x.Group);
                e.Index(x => x.StudentNumber);

                // 在这里因为User中包含两个ICollection<Comment>，并且Comment中包含两个UserId，因此系统无法自动识别，需要在此处声明对应关系
                e.Collection(x => x.Evaluate)
                    .InverseReference(x => x.Evaluator);
                e.Collection(x => x.Evaluated)
                    .InverseReference(x => x.Evaluated);
            });
        }
    }
}
```

接下来创建SampleData.cs静态类，用来初始化数据库。

```
using System;
using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public static class SampleData
    {
        public async static Task InitDB(IServiceProvider service)
        {
            // 获取数据库上下文服务
            var DB = service.GetRequiredService<EvaluateContext>();

            // 获取UserManager服务
            var userManager = service.GetRequiredService<UserManager<User>>();

            // 获取RoleManager服务
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();

            if (DB.Database != null && DB.Database.EnsureCreated())
            {
                // 初始化角色
                await roleManager.CreateAsync(new IdentityRole { Name = "系主任" });
                await roleManager.CreateAsync(new IdentityRole { Name = "组长" });
                await roleManager.CreateAsync(new IdentityRole { Name = "学生" });

                // 初始化系主任，并为其添加系主任角色
                var user = new User { Name = "张某某", UserName = "Admin" };
                await userManager.CreateAsync(user, "Yuuko19931101!@#");
                await userManager.AddToRoleAsync(user, "系主任");
            }
        }
    }
}
```

至此，模型层的工作已经完成。接下来创建AccountController、HomeController、BaseController三个控制器。

首先进入BaseController.cs，将其继承BaseController&lt;TUser, TContext, TKey&gt;

```
using Microsoft.AspNet.Mvc;
using InternEvaluate.Models;

namespace InternEvaluate.Controllers
{
    public class BaseController : BaseController<User, EvaluateContext, string>
    {
    }
}
```

接下来编写AccountController.cs，首先将该类继承BaseController，之后在这里只需要处理渲染登录界面、处理登录请求和注销请求、处理修改密码请求、渲染修改密码界面：

```
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Authorization;

namespace InternEvaluate.Controllers
{
    public class AccountController : BaseController
    {
        // 渲染登录界面
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 处理登录请求
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var result = await SignInManager.PasswordSignInAsync(username, password, false, false);
            if (result.Succeeded)
                return RedirectToAction("Index", "Home");
            else
                return RedirectToAction("Login", "Account");
        }

        // 处理注销请求
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await SignInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // 渲染修改密码界面
        [HttpGet]
        [Authorize]
        public IActionResult Modify()
        {
            return View(CurrentUser);
        }

        // 处理修改密码请求
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Modify(string currentpwd, string newpwd, string confirmpwd)
        {
            // 校验新密码与密码重复是否一致
            if (confirmpwd != newpwd)
                return Content("两次密码输入不一致！");

            // 修改密码
            var result = await UserManager.ChangePasswordAsync(await UserManager.FindByIdAsync(CurrentUser.Id), currentpwd, newpwd);

            if (!result.Succeeded)
                return Content(result.Errors.First().Description);

            // 注销登录
            await SignInManager.SignOutAsync();

            // 跳转到登录界面
            return RedirectToAction("Login", "Account");
        }
    }
}
```

在编辑HomeController之前，我们需要在项目中创建ViewModels文件夹，来放置一个视图模型。在以往的Models文件夹中，我们通常存储的模型是数据模型，与数据库表、字段一一对应，而视图模型则是根据我们业务逻辑的实际需要，来定制一些字段，有些字段可能不是原始数据直接提供的，而需要经过一系列计算，比如下面我们要创建的Student视图模型中的IsChargeman(是否是组长)就需要通过UserManager来获取，这一字段在User中并不直接提供。

Student.cs:

```
namespace InternEvaluate.ViewModels
{
    public class Student
    {
        public string Id { get; set; }
        public string StudentNumber { get; set; }
        public string Name { get; set; }
        public string Class { get; set; }
        public string Group { get; set; }
        public bool IsChargeman { get; set; }
        public int Level_0 { get; set; }
        public int Level_1 { get; set; }
        public int Level_2 { get; set; }
        public int Level_3 { get; set; }
        public int Level_4 { get; set; }
    }
}
```

接下来编辑HomeController.cs，同样需要将继承改为BaseController，在其中编写系主任、学生的操作逻辑：

```
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Microsoft.Data.Entity;
using Microsoft.AspNet.Authorization;
using InternEvaluate.Models;
using InternEvaluate.ViewModels;

namespace InternEvaluate.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        // 欢迎页
        public IActionResult Index()
        {
            return View();
        }

        // 渲染学生用户评价列表页面
        [HttpGet]
        public IActionResult Evaluate()
        {
            var ret = DB.Users
                .Include(x => x.Evaluated)
                .Where(x => x.Group == CurrentUser.Group && x.Id != CurrentUser.Id)
                .ToList();

            return View(ret);
        }

        // 渲染评价页面
        [HttpGet]
        [Authorize(Roles = "学生")]
        public IActionResult Evaluating(string id)
        {
            var user = DB.Users
                .Where(x => x.Id == id)
                .SingleOrDefault();

            if (user == null)
                return Error(404); // 如果id没有对应的学生，则报错

            if (user.Group != CurrentUser.Group || user.Id == CurrentUser.Id)
                return Error(400); // 如果被评价目标是自己或非组内人员，则报错

            ViewBag.Uid = user.Id;

            var comment = DB.Comments
                    .Where(x => x.EvaluatedId == id && x.EvaluatorId == CurrentUser.Id)
                    .SingleOrDefault();

            ViewBag.Name = user.Name;

            return View(comment);
        }

        // 处理评价请求
        [HttpPost]
        [Authorize(Roles = "学生")]
        public IActionResult Evaluating(string uid, Comment comment)
        {
            // 查找之前是否已经评价过
            if (DB.Comments.Any(x => x.EvaluatedId == comment.EvaluatedId && x.EvaluatorId == CurrentUser.Id))
            {
                // 如果已经评价过则实现修改评价的逻辑
                var c = DB.Comments
                    .Where(x => x.EvaluatedId == comment.EvaluatedId && x.EvaluatorId == CurrentUser.Id)
                    .Single();
                c.Level = comment.Level;
                c.Content = comment.Content;
            }
            else
            {
                // 如果没有评价过则创建评价
                comment.EvaluatedId = uid;
                comment.EvaluatorId = CurrentUser.Id;
                if (User.IsInRole("组长"))
                    comment.IsChargeman = true;
                DB.Comments.Add(comment);
            }
            DB.SaveChanges();
            return RedirectToAction("Evaluate", "Home");
        }

        // 渲染学生列表界面
        [Authorize(Roles = "系主任")]
        public async Task<IActionResult> Student()
        {
            // 获取所有的学生与组长
            var users = (await UserManager.GetUsersInRolesAsync("学生"))
                .OrderBy(x => x.StudentNumber)
                .ToList();

            var ret = new List<Student>();
            foreach (var x in users)
            {
                // 获取评分情况
                var comments = DB.Comments
                    .Where(y => y.EvaluatedId == x.Id)
                    .ToList();

                // 根据Users生成视图模型Student
                ret.Add(new Student
                {
                    Id = x.Id,
                    StudentNumber = x.StudentNumber,
                    Name = x.Name,
                    Class = x.Class,
                    Group = x.Group,
                    IsChargeman = await UserManager.IsInRoleAsync(x, "组长"),
                    Level_0 = comments.Where(y => y.Level == Level.不及格).Count(),
                    Level_1 = comments.Where(y => y.Level == Level.及格).Count(),
                    Level_2 = comments.Where(y => y.Level == Level.中等).Count(),
                    Level_3 = comments.Where(y => y.Level == Level.良好).Count(),
                    Level_4 = comments.Where(y => y.Level == Level.优秀).Count()
                });
            }

            return View(ret);
        }

        // 渲染创建学生界面
        [HttpGet]
        [Authorize(Roles = "系主任")]
        public IActionResult CreateStudent()
        {
            return View();
        }

        // 处理创建学生请求
        [HttpPost]
        [Authorize(Roles = "系主任")]
        public async Task<IActionResult> CreateStudent(string username, string password, string studentnumber, string Class, string group, string name, string position)
        {
            var user = new User
            {
                UserName = username,
                Group = group,
                Class = Class,
                Name = name,
                StudentNumber = studentnumber
            };

            // 创建用户
            var result = await UserManager.CreateAsync(user, password);

            // 如果没有成功，则返回错误信息
            if (!result.Succeeded)
                return Content(result.Errors.First().Description);

            // 添加角色
            await UserManager.AddToRoleAsync(user, "学生");

            if (position == "组长")
                await UserManager.AddToRoleAsync(user, "组长");

            return RedirectToAction("Student", "Home");
        }

        [HttpGet]
        [Authorize(Roles = "系主任")]
        public async Task<IActionResult> EditStudent(string id)
        {
            var student = await UserManager.FindByIdAsync(id);

            // 如果没有找到学生则返回错误页面
            if (student == null)
                return Error(404);

            // 将是否为组长保存至ViewBag中
            ViewBag.IsChargeman = await UserManager.IsInRoleAsync(student, "组长");

            return View(student);
        }

        // 处理学生修改请求
        [HttpPost]
        [Authorize(Roles = "系主任")]
        public async Task<IActionResult> EditStudent(string id, string Name, string StudentNumber, string Class, string Group, string Password, string Position)
        {
            var student = await UserManager.FindByIdAsync(id);

            // 如果没有找到学生则返回错误页面
            if (student == null)
                return Error(404);

            if (Position == "组长")
            {
                // 添加组长角色
                await UserManager.AddToRoleAsync(student, "组长");
            }
            else
            {
                // 删除组长角色
                await UserManager.RemoveFromRoleAsync(student, "组长");
            }

            if (!string.IsNullOrEmpty(Password))
            {
                // 生成更改密码的令牌
                var token = await UserManager.GeneratePasswordResetTokenAsync(student);

                // 使用令牌重置密码
                var result = await UserManager.ResetPasswordAsync(student, token, Password);
                
                // 如果修改密码失败则返回错误信息
                if (!result.Succeeded)
                    return Content(result.Errors.First().Description);
            }

            student.Name = Name;
            student.StudentNumber = StudentNumber;
            student.Class = Class;
            student.Group = Group;
            DB.SaveChanges();

            return RedirectToAction("Student", "Home");
        }

        // 处理删除学生请求
        [HttpPost]
        [Authorize(Roles = "系主任")]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            var student = await UserManager.FindByIdAsync(id);

            // 如果没有找到学生则返回错误页面
            if (student == null)
                return Error(404);

            await UserManager.DeleteAsync(student);

            return RedirectToAction("Student", "Home");
        }

        // 渲染评价详情界面
        [HttpGet]
        [Authorize(Roles = "系主任")]
        public IActionResult Detail(string id)
        {
            var ret = DB.Users
                .Include(x => x.Evaluated)
                .ThenInclude(x => x.Evaluator)
                .Where(x => x.Id == id)
                .SingleOrDefault();

            // 如果没有找到用户则返回错误页面
            if (ret == null)
                return Error(404);

            return View(ret);
        }
    }
}
```
其中Include表示加载该模型中的虚成员，在Entity Framework 6中我们习惯使用LazyLoad模式，LazyLoad模式即为需要用时再加载外键所对应的实体，而在Entity Framework 7中，我们被强制使用Eager Load模式，即需要声明可能要使用的虚成员，因此我们需要使用.Include(...)和.ThenInclude(...)来预加载虚成员。

至此控制器全部编写完毕。

我们向wwwroot中添加文件夹：fonts、images、styles、scripts，并向scripts中添加jquery、pie、png。

其中pie是使得旧版本的IE浏览器能够支持部分css3特性、png是使得IE6支持PNG图片的透明显示。

fonts文件夹中的内容则是font-awesome字库

![Upload](http://doc.codecomb.com/file/download/3651cb45-fcb4-463d-8547-fa122d9da63b)

这个字体中包含了一系列的图标，下面菜单中的图标就是使用了font-awesome字库：

![Upload](http://doc.codecomb.com/file/download/d1353aab-b201-41d2-8708-00aca0f1aa98)

向styles中添加icons.css和pie.htc，这里icons.css就是font-awesome附带的样式表，而pie.htc则是pie.js附带的文件。

接下来创建site.css

```
body {
    margin: 0;
    font-family: 'Microsoft YaHei';
    background-color: rgb(241,241,241);
    background-image: url(../images/bg.png)
}

h1, h2, h3, h4, h5, h6 {
    font-weight: 300;
    margin: 0 0 20px;
    color: #111;
    line-height: 1.5em;
}

    h1 a, h2 a, h3 a, h4 a, h5 a, h6 a, h1 a:visited, h2 a:visited, h3 a:visited, h4 a:visited, h5 a:visited, h6 a:visited {
        color: #333;
        transition: color 0.2s linear;
    }

h1 {
    font-size: 48px;
}

h2 {
    font-size: 36px;
}

h3 {
    margin-bottom: 10px;
    font-size: 24px;
}

h4 {
    font-size: 20px;
}

h5 {
    font-size: 18px;
}

h6 {
    font-size: 16px;
}

.container {
    width: 960px;
    margin: 0 auto;
}

.login {
    width: 560px;
    margin: 0 auto;
    color: #fff;
    margin-top: 180px;
}

    .login h2 {
        color: #fff;
    }

.login-logo {
    width: 150px;
    height: 150px;
    float: left;
    margin-right: 35px;
}

.login-title {
    padding: 0;
    margin: 0;
}

.login-textbox {
    padding: 8px;
    margin: 5px 0;
    font-size: 18px;
    outline: 0;
    width: 260px;
    background-color: #222;
    border: none;
    color: #fff;
}

.login-go {
    background-color: #2a3b52;
    margin-top: -35px;
    outline: 0;
    border: 0;
    background-image: url(../images/go.png);
    width: 64px;
    height: 64px;
    float: right;
    margin-left: 20px;
    cursor: pointer;
}

    .login-go:hover {
    background-image: url(../images/go-hover.png);
    }

.footer {
    text-align: center;
    font-size: 12px;
    color: #888;
    margin: 50px 0;
}

.clr {
    clear: both;
}

.left-sidebar {
    background-color: #2a3b52;
    width: 220px;
    padding: 20px;
    height: calc(100% - 40px);
    position: absolute;
    overflow-y: auto;
    left: 0;
    behavior: url(./PIE.htc);
}

.sidebar-subtitle {
    font-size: 14px;
    font-weight: bold;
    color: #aaa;
    margin: 10px 0;
}

.sidebar-menu {
    padding: 10px 20px;
    margin: 0 -20px;
    background-color: rgb(30, 91, 120);
    color: #ddd;
    font-weight: bold;
    text-decoration: none;
    transition: background 0.2s linear, color 0.2s linear;
    border-top: 1px solid rgb(29, 77, 100);
}

    .sidebar-menu:hover, .sidebar.active {
        background-color: rgb(241,241,241);
        color: #333;
    }

.container {
    margin-left: 260px;
}

a, a:visited {
    color: #119eea;
    text-decoration: none;
    outline: 0;
    transition: color 0.2s linear;
}

a:hover {
    color: #3eb2f1;
    text-decoration: none;
}

a.dark, a.dark:visited {
    color: #0dc6fb;
    text-decoration: none;
}

a.dark:focus, a.dark:hover {
    color: #25ccfb;
    text-decoration: none;
}

.wrap-cont {
    padding: 30px;
    height: calc(100% - 60px);
    overflow: auto;
    position: absolute;
    *left: 320px;
    width: calc(100% - 320px);
    *width: 900px;
    behavior: url(./PIE.htc);
}

.detail-table {
    width: 100%;
}

.detail-table td, .detail-table th {
    padding: 16px 10px;
    border: 1px solid #ddd;
}

.row-title {
    background-color: #f0f0f0;
    width: 180px;
}

table {
    table-layout: auto;
    width: 100%;
    border-collapse: collapse;
    border-spacing: 0;
}

.textbox {
    margin: 0;
    -webkit-box-sizing: content-box;
    -moz-box-sizing: content-box;
    box-sizing: content-box;
    height: 20px;
    line-height: 20px;
    outline: 0;
    padding: 4px;
    border: 1px solid #ccc;
    transition: box-shadow .2s linear,background .2s linear;
    background: #f2f2f2;
    max-width:100%;
    _width:120px;
    *width:120px;
    behavior:url(./PIE.htc);
}

.textbox:hover {
    border-color: #b8b8b8;
    background: #fcfcfc;
}

.textbox:focus {
    border-color: #ffdf00;
    background: #fcfcfc;
    -webkit-box-shadow: 0 0 10px #ffdf00;
    box-shadow: 0 0 10px #ffdf00;
    behavior:url(./PIE.htc);
}

.textbox.error {
    border-color: #ff696d;
    background: #fcfcfc;
    -webkit-box-shadow: 0 0 10px rgb(255, 128, 128);
    box-shadow: 0 0 10px rgb(255, 128, 128);
    behavior:url(./PIE.htc);
}

.w-0-6 {
    width: 60px!important;
}

.w-0-8 {
    width: 80px!important;
}

.w-1 {
    width: 100px!important;
}

.w-2 {
    width: 200px!important;
}

.w-3 {
    width: 300px!important;
}

.info {
    width: 600px;
    padding: 30px;
    background-color: #fff;
    color: #000;
    margin: 0 auto;
    margin-top: 180px;
}

.btn{
    padding: 5px 10px;
    border: 3px solid #000;
    background-color: #fff;
    color: #000;
    font-weight: bold;
    transition: background 0.2s linear;
    cursor: pointer;
    font-size: 12px;
}

    .btn:hover {
        color: #000;
        background-color: #ddd;
    }

    .btn.inverse {
        color: #fff;
        background-color: #000
    }

        .btn.inverse:hover {
            color: #fff;
            background-color: #2a3b52;
        }

    .btn.red {
        color: #fff;
        border: 3px solid red;
        background-color: #ff696d
    }

        .btn.red:hover {
            color: #fff;
            background-color: red;
        }

.ico {
    margin-right: 10px;
}

.search {
    padding: 20px;
    background-color: #fff;
    margin-bottom: 20px;
}

select {
    display: inline-block;
    vertical-align: middle;
    border: 1px solid #cccccc;
    background-color: #ffffff;
    height: 30px;
    line-height: 30px;
    font-size: 14px;
    margin: 0;
    width: 150px;
}

.table {
    table-layout: auto;
}

.table th {
    text-align: left;
    border-bottom: 3px solid rgb(30, 91, 120);
}

.table th, .table td {
    padding: 8px;
    text-overflow: ellipsis;  
}

.pager-item, .pager-item:visited, .pager-item:active {
    color: #fff;
    background-color: #2a3b52;
    transition: background 0.2s linear, color 0.2s linear;
    float: left;
    font-size: 18px;
    padding: 10px;
    margin-right: 1px;
}

.pager {
    padding: 20px 0;
}

.pager-item.active, .pager-item:hover {
    background-color: rgb(30, 91, 120);
    color: #fff;
}

.message-bg {
    background-color: rgba(0,0,0,0.5);
    width: 100%;
    height: 100%;
    z-index: 0;
    position: absolute;
    opacity: 0;
    transition: opacity 0.2s linear;
    behavior: url(./PIE.htc);
}

    .message-bg.active {
        opacity: 1;
    }

.message-outer {
    top: 50%;
    width: 100%;
    position: absolute;
    background-color: #fff;
    opacity: 0;
    transform: scale(1.2);
    transition: opacity 0.2s linear, transform 0.2s linear;
    behavior: url(./PIE.htc);
}

.message-outer {
    opacity: 1;
    transform: scale(1);
}

.message-container {
    width: 600px;
    padding: 30px;
    margin: 0 auto;
}

.table tbody tr:nth-child(odd) {
    background-color: #fafafa;
}

.message-buttons {
    margin-top: 20px;
}

.table tbody tr {
    transition: background 0.2s linear, color 0.2s linear;
}

.table a {
    color: #333;
}

.table tbody tr:hover {
    background-color: rgb(30, 91, 120);
    color: #fff;
}

.table tbody tr:hover a {
    color: #fff;
}

.sub-menu-item {
    font-size: 16px;
    color: #119eea!important;
}

img {
    max-width: 100%;
}

.img-cert {
    width: 480px;
}

ul {
    list-style: none;
    margin: 0;
}

textarea {
    width: 380px;
    height: 100px;
}
```

这些工作完成后，我们在项目根目录下创建Views文件夹

首先在Views下创建_ViewStart.cshtml：

```
@{ 
    Layout = "~/Views/Shared/_Layout.cshtml";
}
```

接下来创建_ViewImports.cshtml：

```
@using InternEvaluate.Models
@using InternEvaluate.ViewModels
@using Microsoft.AspNet.Identity
@addTagHelper "*, Microsoft.AspNet.Mvc.TagHelpers"
@inject  Microsoft.Framework.DependencyInjection.User<string, User, UserManager<User>> User
```

@inject是从Service中取出服务，这里使用的是CodeComb.vNext扩展的User帮助类，Microsoft.Framework.DependencyInjection.User需要传递三个类型参数，分别为TKey、TUser、TUserManager。代表了用户主关键字类型（直接继承IdentityUser时为string），用户模型和UserManager。

在Views下创建三个文件夹：Account、Home、Shared。

首先进入Shared创建_Layout.cshtml：

```
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>@ViewBag.Title - 毕业实习互评系统</title>
    <link href="~/styles/icons.css" rel="stylesheet" />
    <link href="~/styles/site.css" rel="stylesheet" type="text/css" />
    <script src="~/scripts/jquery-1.10.2.min.js"></script>
    <script src="~/scripts/site.js"></script>
    <!--[if lte IE 9]>
    <script src="/scripts/pie.js"></script>
    <![endif]-->
    <!--[if lte IE 6]>
    <script src="/scripts/png.js"></script>
    <![endif]-->
</head>
<body>
    <div class="left-sidebar">
        <div class="sidebar-subtitle">毕业实习互评系统</div>
        <a asp-action="Index" asp-controller="Home"><div class="sidebar-menu"><i class="fa fa-home icon ico"></i>后台首页</div></a>
        @if (User.IsInRoles("组长, 学生"))
        {
            <div class="sidebar-subtitle">学生评价</div>
            <a asp-action="Evaluate" asp-controller="Home"><div class="sidebar-menu"><i class="fa fa-comment icon ico"></i>组内评价</div></a>
        }
        else
        {
            <div class="sidebar-subtitle">评价管理</div>
            <a asp-action="Student" asp-controller="Home"><div class="sidebar-menu"><i class="fa fa-users icon ico"></i>学生管理</div></a>
            <a asp-action="CreateStudent" asp-controller="Home"><div class="sidebar-menu"><i class="fa fa-user-plus icon ico"></i>添加学生</div></a>
        }
        <div class="sidebar-subtitle">其他操作</div>
        <a asp-action="Modify" asp-controller="Account"><div class="sidebar-menu"><i class="fa fa-cog icon ico"></i>修改密码</div></a>
        <a href="javascript: $('#frmLogout').submit()"><div class="sidebar-menu"><i class="fa fa-sign-out icon ico"></i>退出系统</div></a>
        <form asp-action="Logout" asp-controller="Account" method="post" id="frmLogout"></form>
    </div>
    <div class="container">
        <div class="wrap-cont">
            @RenderBody()
            <div class="footer">Copyright © 2015 Harbin Code Comb Technology Co., Ltd. All rights reserved.</div>
        </div>
    </div>
</body>
</html>
```

接下来进入Account文件夹创建Login.cshtml：

```
@{
    ViewBag.Title = "登录";
    Layout = null;
}
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>@ViewBag.Title - 毕业实习学生互评系统</title>
    <link href="~/styles/site.css" rel="stylesheet" type="text/css" />
    <!--[if lte IE 9]>
    <script src="/Scripts/pie.js"></script>
    <![endif]-->
    <!--[if lte IE 6]>
    <script src="/Scripts/png.js"></script>
    <![endif]-->

</head>
<body style="background-color:#2a3b52;background-image:none;">
    <div class="login">
        <form asp-action="Login" asp-controller="Account">
            <img class="login-logo" src="~/Images/Logo.png" alt="毕业实习学生互评系统" />
            <h2 class="login-title">毕业实习互评系统</h2>
            <input type="text" class="login-textbox" placeholder="用户名" name="Username" />
            <br /><input type="password" class="login-textbox" placeholder="密码" name="Password" />
            <input type="submit" value="" class="login-go" />
        </form>
    </div>
    <div class="clr"></div>
    <div class="footer">
        Copyright © 2015 Harbin Code Comb Technology Co., Ltd. All rights reserved.
    </div>
</body>
</html>
```

再在Account文件夹中创建Modify.cshtml：

```
@{ 
    ViewBag.Title = "修改密码";
}

<h2>@ViewBag.Title</h2>

<form asp-action="Modify" asp-controller="Account" method="post">
    <table class="detail-table">
        <tr>
            <td class="row-title">旧密码</td>
            <td><input type="password" name="currentpwd" /></td>
        </tr>
        <tr>
            <td class="row-title">新密码</td>
            <td><input type="password" name="newpwd" /></td>
        </tr>
        <tr>
            <td class="row-title">新密码重复</td>
            <td><input type="password" name="confirmpwd" /></td>
        </tr>
    </table>
    <p>
        <input type="submit" class="btn inverse" value="提交" />
    </p>
</form>
```

返回Views下，进入Home文件夹，创建视图Index.cshtml

```
@{ 
    ViewBag.Title = "首页";
}
<h2>欢迎您来到毕业实习学生互评系统</h2>
<p>请在本系统中对本组同学进行评估，评定等级分为：</p>
<p>不及格：无法完成组长安排的任务、没有按时出勤、态度不积极</p>
<p>及格：无法完成组长安排的任务，有缺勤情况，态度一般</p>
<p>中等：完成了部分组长安排的任务，偶尔缺勤，态度较好</p>
<p>良好：任务完成情况较好，保证出勤，积极与组内沟通</p>
<p>优秀：任务完全完成，满勤，能够积极帮助组内其他成员</p>
```

创建Evaluate.cshtml

```
@model IEnumerable<User>
@{ 
    ViewBag.Title = "学生互评";
}
<table class="table">
    <thead>
        <tr>
            <th>学号</th>
            <th>姓名</th>
            <th>班级</th>
            <th>组</th>
            <th>我的评级</th>
            <th>我的评价</th>
            <th>操作</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var x in Model)
        { 
            <tr>
                <td>@x.StudentNumber</td>
                <td>@x.Name</td>
                <td>@x.Class</td>
                <td>@x.Group</td>
                @if (x.Evaluated.Any(y => y.EvaluatorId == User.Current.Id))
                {
                    <td>@x.Evaluated.Where(y => y.EvaluatorId == User.Current.Id).Single().Level</td>
                    <td>@x.Evaluated.Where(y => y.EvaluatorId == User.Current.Id).Single().Content</td>
                }
                else
                {
                    <td>-</td>
                    <td>-</td>
                }
                <td>
                    <a asp-action="Evaluating" asp-controller="Home" asp-route-id="@x.Id">评价</a>
                </td>
            </tr>
        }
    </tbody>
</table>
```

创建Evaluating.cshtml：

```
@model Comment
@{ 
    ViewBag.Title = "评价" + ViewBag.Name;
}
<h2>@ViewBag.Title</h2>

<form asp-action="Evaluating" asp-controller="Home" asp-route-uid="@ViewBag.Uid" method="post">
    <table class="detail-table">
        <tr>
            <td class="row-title">评价等级</td>
            <td>
                <select asp-for="Level">
                    <option>优秀</option>
                    <option>良好</option>
                    <option>中等</option>
                    <option>及格</option>
                    <option>不及格</option>
                </select>
            </td>
        </tr>
        <tr>
            <td class="row-title">评价内容</td>
            <td>
                <textarea asp-for="Content" placeholder="选填"></textarea>
            </td>
        </tr>
    </table>
    <p>
        <input type="submit" class="btn inverse" value="提交" />
    </p>
</form>
```

创建CreateStudent.cshtml：

```
@{ 
    ViewBag.Title = "创建学生";
}
<h2>@ViewBag.Title</h2>
<form asp-action="CreateStudent" asp-controller="Home">
    <table class="detail-table">
        <tr>
            <td class="row-title">用户名</td>
            <td><input type="text" name="username" /></td>
        </tr>
        <tr>
            <td class="row-title">密码</td>
            <td><input type="password" name="password" /></td>
        </tr>
        <tr>
            <td class="row-title">姓名</td>
            <td><input type="text" name="name" /></td>
        </tr>
        <tr>
            <td class="row-title">学号</td>
            <td><input type="text" name="studentnumber" /></td>
        </tr>
        <tr>
            <td class="row-title">班级</td>
            <td><input type="text" name="class" /></td>
        </tr>
        <tr>
            <td class="row-title">组</td>
            <td><input type="text" name="group" /></td>
        </tr>
        <tr>
            <td class="row-title">职务</td>
            <td>
                <select name="position">
                    <option>组员</option>
                    <option>组长</option>
                </select>
            </td>
        </tr>
    </table>
    <p>
        <input type="submit" class="btn inverse" value="提交" />
    </p>
</form>
```

创建EditStudent.cshtml：

```
@model User
@{
    ViewBag.Title = "编辑学生";
}
<h2>@ViewBag.Title</h2>
<form asp-action="EditStudent" asp-controller="Home">
    <table class="detail-table">
        <tr>
            <td class="row-title">用户名</td>
            <td>@Model.UserName</td>
        </tr>
        <tr>
            <td class="row-title">新密码</td>
            <td><input type="password" name="password" placeholder="不修改请留空" /></td>
        </tr>
        <tr>
            <td class="row-title">姓名</td>
            <td><input type="text" name="name" asp-for="Name" /></td>
        </tr>
        <tr>
            <td class="row-title">学号</td>
            <td><input type="text" name="studentnumber" asp-for="StudentNumber" /></td>
        </tr>
        <tr>
            <td class="row-title">班级</td>
            <td><input type="text" name="class" asp-for="Class" /></td>
        </tr>
        <tr>
            <td class="row-title">组</td>
            <td><input type="text" name="group" asp-for="Group" /></td>
        </tr>
        <tr>
            <td class="row-title">职务</td>
            <td>
                <select name="position">
                    <!option @(ViewBag.IsChargeman ? "" : "selected")>组员</!option>
                    <!option @(ViewBag.IsChargeman ? "selected " : "")>组长</!option>
                </select>
            </td>
        </tr>
    </table>
    <p>
        <input type="submit" class="btn inverse" value="提交" />
    </p>
</form>
```

创建Student.cshtml：

```
@model IEnumerable<Student>
@{ 
    ViewBag.Title = "学生管理";
}

<h2>@ViewBag.Title</h2>
<table class="detail-table">
    <thead>
        <tr>
            <th rowspan="2">学号</th>
            <th rowspan="2">姓名</th>
            <th rowspan="2">班级</th>
            <th rowspan="2">组</th>
            <th colspan="5">评级</th>
            <th rowspan="2">操作</th>
        </tr>
        <tr>
            <th>不及格</th>
            <th>及格</th>
            <th>中等</th>
            <th>良好</th>
            <th>优秀</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var x in Model)
        { 
            <tr>
                <td>@x.StudentNumber</td>
                <td>@(x.IsChargeman? "*" : "")@x.Name</td>
                <td>@x.Class</td>
                <td>@x.Group</td>
                <td>@x.Level_0</td>
                <td>@x.Level_1</td>
                <td>@x.Level_2</td>
                <td>@x.Level_3</td>
                <td>@x.Level_4</td>
                <td>
                    <a asp-action="Detail" asp-controller="Home" asp-route-id="@x.Id">查看详评</a>
                    <a asp-action="EditStudent" asp-controller="Home" asp-route-id="@x.Id">编辑学生</a>
                    <a href="javascript:Delete('@x.Id');">删除学生</a>
                </td>
            </tr>
        }
    </tbody>
</table>
<form asp-action="DeleteStudent" asp-controller="Home" method="post" id="frmDeleteStudent()">
    <input type="hidden" id="id" name="id" />
</form>
<script>
    function Delete(id)
    {
        $('#id').val(id);
        $('#frmDeleteStudent').submit();
    }
</script>
```

创建Detail.cshtml：

```
@model User
@{ 
    ViewBag.Title = Model.Name + "的评价";
}

<table class="detail-table">
    @foreach (var c in Model.Evaluated.OrderByDescending(x => x.IsChargeman))
    {
        <tr>
            <td>
                @if (c.IsChargeman)
                {
                    <p><b>来自 @c.Evaluator.Name 的评价：@c.Level</b></p>
                }
                else
                {
                    <p><b>来自 组员 的评价：@c.Level</b></p>
                }
                <p>@c.Content</p>
            </td>
        </tr>
    }
</table>
```

至此视图层的工作完成！

接下来配置Startup.cs：

```
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Dnx.Runtime;
using Microsoft.Data.Entity;
using InternEvaluate.Models;

namespace InternEvaluate
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // 获取应用程序环境服务
            var appEnv = services.BuildServiceProvider().GetRequiredService<IApplicationEnvironment>();

            // 添加Entity Framework服务
            services.AddEntityFramework()
                .AddSqlite()
                .AddDbContext<EvaluateContext>(x => x.UseSqlite("Data source=" + appEnv.ApplicationBasePath + "/evaluate.db")); // SQLite的数据库连接字符串

            // 添加Identity服务
            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<EvaluateContext>()
                .AddDefaultTokenProviders();

            // 添加MVC服务
            services.AddMvc();           
            
            // 添加CodeComb扩展的CurrentUser服务，在前台可以直接使用已经扩展的User帮助类
            services.AddCurrentUser<string, User>();
        }

        public async void Configure(IApplicationBuilder app)
        {
            // 使用StaticFiles就会为wwroot下的文件添加到路由规则中
            app.UseStaticFiles();

            // 使用Identity中间件可以使标记有[Authorize]之类的action在未登录时跳转到登录页面
            app.UseIdentity();

            // 使用MVC中间件，并配置默认路由规则
            app.UseMvc(x => x.MapRoute("default", "{controller=Home}/{action=Index}/{id?}"));

            // 初始化数据库并添加初始数据
            await SampleData.InitDB(app.ApplicationServices);
        }
    }
}
```

全部工作完成，开始调试吧！

![Upload](http://doc.codecomb.com/file/download/ce8341e8-d129-44a7-a851-51fee2abab21)
