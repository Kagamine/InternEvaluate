using Microsoft.AspNet.Mvc;
using InternEvaluate.Models;

namespace InternEvaluate.Controllers
{
    public class BaseController : BaseController<User, EvaluateContext, string>
    {
    }
}
