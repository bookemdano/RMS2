using System;
using Jiranator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Burr
{
    [TestClass]
    public class JiraIssueUnitTest
    {
        [TestMethod]
        public void CalcStatusDocInProgress()
        {
            var issue = JiraIssue.ForTesting(null, "Test1", JiraIssue.StatusEnum.InProgress, "Story");
            var sub = JiraIssue.ForTesting(null, "DOC", JiraIssue.StatusEnum.InProgress, "Doc");
            issue.AddSubtask(sub);
            sub = JiraIssue.ForTesting(null, "Implement", JiraIssue.StatusEnum.Resolved, "Task");
            issue.AddSubtask(sub);
            var calced = issue.CalcedStatus;
            Assert.AreEqual(JiraIssue.StatusEnum.Doc, calced);
        }
        [TestMethod]
        public void CalcStatusDocOpen()
        {
            var issue = JiraIssue.ForTesting(null, "Test1", JiraIssue.StatusEnum.CodeReview, "Story");
            var sub = JiraIssue.ForTesting(null, "DOC", JiraIssue.StatusEnum.Open, "Doc");
            issue.AddSubtask(sub);
            sub = JiraIssue.ForTesting(null, "Implement", JiraIssue.StatusEnum.CodeReview, "Task");
            issue.AddSubtask(sub);
            var calced = issue.CalcedStatus;
            Assert.AreEqual(JiraIssue.StatusEnum.Doc, calced);
        }
    }
}
