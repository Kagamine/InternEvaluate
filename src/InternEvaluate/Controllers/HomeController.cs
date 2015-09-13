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
