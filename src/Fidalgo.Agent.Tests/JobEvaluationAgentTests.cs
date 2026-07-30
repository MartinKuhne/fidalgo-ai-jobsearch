using Fidalgo.Agent.Agents;
using Fidalgo.Shared.Storage;
using Xunit;

namespace Fidalgo.Agent.Tests;

public class JobEvaluationAgentTests
{
    private static JobEntity SampleJob() => new()
    {
        InternalId = Guid.NewGuid(),
        Email = "user@example.com",
        Employer = "Acme Corp",
        Title = "Software Architect",
        Description = "Lead architecture for a distributed cloud platform.",
    };

    [Fact]
    public void BuildPrompt_AlwaysIncludesEmailResumeAndJobFields()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: null,
            careerGoals: null,
            job: job);

        Assert.Contains("user@example.com", prompt);
        Assert.Contains("My resume", prompt);
        Assert.Contains("Software Architect", prompt);
        Assert.Contains("Acme Corp", prompt);
        Assert.Contains("Lead architecture for a distributed cloud platform.", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsCareerStorySection_WhenNotProvided()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: null,
            careerGoals: null,
            job: job);

        Assert.DoesNotContain("Career Story:", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsCareerStorySection_WhenWhitespace()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: "   \n\t  ",
            careerGoals: null,
            job: job);

        Assert.DoesNotContain("Career Story:", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesCareerStory_WhenProvided()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: "I started as a backend engineer and grew into architecture.",
            careerGoals: null,
            job: job);

        Assert.Contains("Career Story:", prompt);
        Assert.Contains("I started as a backend engineer and grew into architecture.", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsCareerGoalsSection_WhenNotProvided()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: null,
            careerGoals: null,
            job: job);

        Assert.DoesNotContain("Career Goals:", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesCareerGoals_WhenProvided()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: null,
            careerGoals: "Move into platform engineering leadership within 18 months.",
            job: job);

        Assert.Contains("Career Goals:", prompt);
        Assert.Contains("Move into platform engineering leadership within 18 months.", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesBothSections_WhenBothProvided()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: "Story text",
            careerGoals: "Goals text",
            job: job);

        Assert.Contains("Career Story:", prompt);
        Assert.Contains("Story text", prompt);
        Assert.Contains("Career Goals:", prompt);
        Assert.Contains("Goals text", prompt);
    }

    [Fact]
    public void BuildPrompt_InstructionsMentionCareerStoryAndGoals()
    {
        var job = SampleJob();

        var prompt = JobEvaluationAgent.BuildPrompt(
            email: "user@example.com",
            resume: "My resume",
            careerStory: null,
            careerGoals: null,
            job: job);

        // The instructions should reference the new sources of context.
        Assert.Contains("career story", prompt);
        Assert.Contains("career goals", prompt);
    }
}
