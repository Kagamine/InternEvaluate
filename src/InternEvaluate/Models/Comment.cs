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
