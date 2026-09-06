using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SwitchPoint.Calculation.Numerics;

namespace SwitchPoint.Calculation.Tests.Numerics;

public class RootFinderTests
{
    [Fact]
    public void Finds_square_root_of_two()
    {
        RootResult r = RootFinder.Solve(x => (x * x) - 2m, 0m, 2m, RootOptions.ForRates);
        Assert.InRange(Math.Abs(r.Value - DecimalMath.Sqrt(2m)), 0m, 1e-12m);
        Assert.True(r.Iterations < 40);
        Assert.Equal(0, r.BracketExpansions);
    }

    [Fact]
    public void Returns_exact_endpoint_when_it_is_a_root()
    {
        RootResult r = RootFinder.Solve(x => x - 3m, 3m, 10m);
        Assert.Equal(3m, r.Value);
        Assert.Equal(0, r.Iterations);
    }

    [Fact]
    public void Expands_bracket_when_root_lies_outside()
    {
        // Root at x = 25, bracket starts at [0, 1].
        RootResult r = RootFinder.Solve(x => x - 25m, 0m, 1m, RootOptions.ForRates);
        Assert.InRange(Math.Abs(r.Value - 25m), 0m, 1e-10m);
        Assert.True(r.BracketExpansions > 0);
    }

    [Fact]
    public void Throws_when_no_root_is_bracketable()
    {
        Assert.Throws<RootNotBracketedException>(() => RootFinder.Solve(x => (x * x) + 1m, -1m, 1m));
    }

    [Fact]
    public void Throws_when_iteration_cap_is_hit()
    {
        RootOptions strict = new(ValueTolerance: 0m, ArgumentTolerance: 0m, MaxIterations: 3);
        Assert.Throws<RootNotConvergedException>(() => RootFinder.Solve(x => DecimalMath.Exp(x) - 5m, 0m, 3m, strict));
    }

    [Fact]
    public void Rejects_inverted_bracket() =>
        Assert.Throws<ArgumentException>(() => RootFinder.Solve(x => x, 1m, 0m));

    [Fact]
    public void Solves_a_projection_style_money_objective_to_half_a_penny()
    {
        // Solve for the growth rate that turns £100,000 into £250,000 over 20 years.
        RootResult r = RootFinder.Solve(g => (100_000m * DecimalMath.Pow(1m + g, 20m)) - 250_000m, 0m, 0.10m);
        Assert.InRange(Math.Abs(r.Residual), 0m, 0.005m);
        // Analytical answer: 2.5^(1/20) − 1 ≈ 4.6880%.
        Assert.InRange(r.Value, 0.04687m, 0.04689m);
    }

    [Fact]
    public void Handles_decreasing_functions()
    {
        RootResult r = RootFinder.Solve(x => 10m - (2m * x), 0m, 100m, RootOptions.ForRates);
        Assert.InRange(Math.Abs(r.Value - 5m), 0m, 1e-12m);
    }

    [Property(MaxTest = 200)]
    public Property Recovers_planted_linear_root()
    {
        Gen<decimal> roots = Gen.Choose(-500_000, 500_000).Select(i => i / 100m);
        Gen<decimal> slopes = Gen.Choose(1, 10_000).Select(i => i / 100m);
        return Prop.ForAll(roots.ToArbitrary(), slopes.ToArbitrary(), (root, slope) =>
        {
            RootResult r = RootFinder.Solve(x => slope * (x - root), -1m, 1m, RootOptions.ForRates);
            return Math.Abs(r.Value - root) < 1e-9m;
        });
    }

    [Property(MaxTest = 100)]
    public Property Recovers_planted_cubic_root()
    {
        Gen<decimal> roots = Gen.Choose(-1000, 1000).Select(i => i / 10m);
        return Prop.ForAll(roots.ToArbitrary(), root =>
        {
            decimal Cubic(decimal x) => ((x - root) * (x - root) * (x - root)) + (x - root);
            RootResult r = RootFinder.Solve(Cubic, root - 3m, root + 5m, RootOptions.ForRates);
            return Math.Abs(r.Value - root) < 1e-9m;
        });
    }
}
