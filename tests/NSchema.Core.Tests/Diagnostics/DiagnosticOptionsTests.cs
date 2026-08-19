using NSchema.Project.Nsql;

namespace NSchema.Tests.Diagnostics;

/// <summary>
/// Enforcement: a producer reports a finding at the severity it judges natural, and this is where that
/// judgement is overridden — by code for one finding, by source for a producer's whole family.
/// </summary>
public sealed class DiagnosticOptionsTests
{
    private readonly DiagnosticOptions _sut = new();

    private static Diagnostic Hazard(string code = "risky-type-change") =>
        Diagnostic.Warning("data-hazards", code, "the cast may fail");

    [Fact]
    public void Apply_WithNothingConfigured_LeavesEveryFindingAsReported()
    {
        // Act
        var applied = _sut.Apply([Hazard()]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData(PolicyEnforcement.Error, DiagnosticSeverity.Error)]
    [InlineData(PolicyEnforcement.Warn, DiagnosticSeverity.Warning)]
    [InlineData(PolicyEnforcement.Allow, DiagnosticSeverity.Info)]
    public void Apply_ByCode_ReportsAtTheConfiguredSeverity(PolicyEnforcement enforcement, DiagnosticSeverity expected)
    {
        // Arrange
        _sut.ByCode["risky-type-change"] = enforcement;

        // Act
        var applied = _sut.Apply([Hazard()]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Severity.ShouldBe(expected);
    }

    [Fact]
    public void Apply_Ignored_DropsTheFindingEntirely()
    {
        // Arrange
        _sut.ByCode["risky-type-change"] = PolicyEnforcement.Ignore;

        // Act & Assert
        _sut.Apply([Hazard()]).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_BySource_CoversTheProducersWholeFamily()
    {
        // Arrange — a policy reports a family of findings, so configuring the producer configures all of them
        // without naming each code.
        _sut.BySource["data-hazards"] = PolicyEnforcement.Allow;

        // Act
        var applied = _sut.Apply([Hazard("risky-type-change"), Hazard("column-becomes-required")]).ToList();

        // Assert
        applied.Count.ShouldBe(2);
        applied.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void Apply_ByCode_WinsOverItsSource()
    {
        // Arrange — silence a producer, but keep one of its findings blocking.
        _sut.BySource["data-hazards"] = PolicyEnforcement.Ignore;
        _sut.ByCode["risky-type-change"] = PolicyEnforcement.Error;

        // Act
        var applied = _sut.Apply([Hazard("risky-type-change"), Hazard("column-becomes-required")]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Apply_LeavesOtherProducersAlone()
    {
        // Arrange
        _sut.BySource["data-hazards"] = PolicyEnforcement.Ignore;

        // Act
        var applied = _sut.Apply([Hazard(), Diagnostic.Error("plan", "missing-dialect", "no dialect")]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Code.ShouldBe("missing-dialect");
    }

    [Fact]
    public void Apply_CannotLowerAStructuralError()
    {
        // Arrange — the finding means NSchema cannot do what was asked, so permitting it would not make it work.
        _sut.ByCode["missing-dialect"] = PolicyEnforcement.Warn;

        // Act
        var applied = _sut.Apply([Diagnostic.Error("plan", "missing-dialect", "no dialect")]).ToList();

        // Assert
        applied.ShouldContain(d => d.Code == "missing-dialect" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Apply_CannotSilenceAStructuralError()
    {
        // Arrange
        _sut.ByCode["missing-dialect"] = PolicyEnforcement.Ignore;

        // Act
        var applied = _sut.Apply([Diagnostic.Error("plan", "missing-dialect", "no dialect")]).ToList();

        // Assert — it stands rather than vanishing, so the run still fails for the reason it should, and the
        // configuration that could not be honoured is said out loud rather than quietly dropped.
        applied.Select(d => d.Code).ShouldBe(["missing-dialect", "cannot-be-lowered"]);
        applied[1].Message.ShouldContain("missing-dialect");
    }

    [Fact]
    public void Apply_SaysAConfigurationCannotBeHonoured_OncePerCode()
    {
        // Arrange — the same finding can occur many times over; the note about the configuration should not.
        _sut.ByCode["missing-dialect"] = PolicyEnforcement.Ignore;
        var findings = new[]
        {
            Diagnostic.Error("plan", "missing-dialect", "no dialect"),
            Diagnostic.Error("plan", "missing-dialect", "no dialect"),
        };

        // Act
        var applied = _sut.Apply(findings).ToList();

        // Assert
        applied.Count(d => d.Code == "cannot-be-lowered").ShouldBe(1);
    }

    [Fact]
    public void Apply_SaysNothing_WhenTheConfigurationIsHonoured()
    {
        // Arrange
        _sut.ByCode["risky-type-change"] = PolicyEnforcement.Ignore;
        _sut.ByCode["missing-dialect"] = PolicyEnforcement.Error;

        // Act — one lowering that is allowed, one raising that always is.
        var applied = _sut.Apply([Hazard(), Diagnostic.Error("plan", "missing-dialect", "no dialect")]).ToList();

        // Assert
        applied.ShouldNotContain(d => d.Code == "cannot-be-lowered");
    }

    [Fact]
    public void Apply_CanLowerAnAdvisoryError()
    {
        // Arrange — the change is expressible and correct; whether it is wanted is the caller's call.
        _sut.ByCode["destructive-change"] = PolicyEnforcement.Allow;
        var destructive = Diagnostic.Error("destructive-actions", "destructive-change", "drops a table")
            with
        { Kind = DiagnosticKind.Advisory };

        // Act
        var applied = _sut.Apply([destructive]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Info);
    }

    [Fact]
    public void Apply_CanRaiseAStructuralFinding()
    {
        // Arrange — asking for more scrutiny is always safe, so nothing is protected from being raised.
        _sut.ByCode["undeclared-foreign-key-target"] = PolicyEnforcement.Error;
        var advisory = Diagnostic.Warning("structural-integrity", "undeclared-foreign-key-target", "may already exist");

        // Act
        var applied = _sut.Apply([advisory]).ToList();

        // Assert
        applied.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Apply_CanLowerAnAdvisoryWarning()
    {
        // Arrange — reporting below error severity declares a finding advisory, so a warning is silenceable.
        _sut.ByCode["risky-type-change"] = PolicyEnforcement.Ignore;

        // Act & Assert
        _sut.Apply([Hazard()]).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_CannotLowerAStructuralWarning()
    {
        // Arrange — the kind governs at every severity, not just at error, so that a finding raised later (by a
        // warnings-as-errors setting, say) does not change what a caller is allowed to silence.
        _sut.ByCode["structural-warning"] = PolicyEnforcement.Ignore;
        var structural = Diagnostic.Warning("plan", "structural-warning", "cannot be silenced")
            with
        { Kind = DiagnosticKind.Structural };

        // Act
        var applied = _sut.Apply([structural]).ToList();

        // Assert
        applied.ShouldContain(d => d.Code == "structural-warning" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Apply_RaisingThenLoweringAnAdvisoryFinding_StaysAllowed()
    {
        // Arrange — the kind is fixed when the finding is created, so promoting it to an error does not make it
        // unsilenceable the way deriving the rule from severity would.
        _sut.ByCode["risky-type-change"] = PolicyEnforcement.Ignore;
        var promoted = Hazard() with { Severity = DiagnosticSeverity.Error };

        // Act & Assert
        _sut.Apply([promoted]).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_KeepsASpecializedFindingSpecialized()
    {
        // Arrange — enforcement rewrites the severity, so a producer's structured finding must survive it.
        _sut.ByCode["syntax-error"] = PolicyEnforcement.Warn;
        var typed = new NsqlDiagnostic("syntax", "syntax-error", "unexpected token",
            DiagnosticSeverity.Error, new SourcePosition(Offset: 4, Line: 2, Column: 1))
        { Kind = DiagnosticKind.Advisory };

        // Act
        var applied = _sut.Apply([typed]).ToList();

        // Assert
        var single = applied.ShouldHaveSingleItem().ShouldBeOfType<NsqlDiagnostic>();
        single.Severity.ShouldBe(DiagnosticSeverity.Warning);
        single.Position.Line.ShouldBe(2);
    }
}
