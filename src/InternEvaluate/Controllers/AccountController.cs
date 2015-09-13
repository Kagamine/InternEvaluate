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
