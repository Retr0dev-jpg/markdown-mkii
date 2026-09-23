namespace MarkdownMkII.Core.Text;

public static class MathText
{
    private static readonly System.Text.RegularExpressions.Regex Command = new(
        @"\\(?:mathbb\{[RNZQC]\}|[A-Za-z]+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly (string From, string To)[] Tokens =
    [
        ("\\alpha", "α"), ("\\beta", "β"), ("\\gamma", "γ"), ("\\delta", "δ"),
        ("\\epsilon", "ε"), ("\\theta", "θ"), ("\\lambda", "λ"), ("\\mu", "μ"),
        ("\\pi", "π"), ("\\sigma", "σ"), ("\\phi", "φ"), ("\\omega", "ω"),
        ("\\Gamma", "Γ"), ("\\Delta", "Δ"), ("\\Theta", "Θ"), ("\\Lambda", "Λ"),
        ("\\Pi", "Π"), ("\\Sigma", "Σ"), ("\\Omega", "Ω"),
        ("\\sum", "∑"), ("\\prod", "∏"), ("\\int", "∫"), ("\\infty", "∞"),
        ("\\pm", "±"), ("\\times", "×"), ("\\cdot", "·"), ("\\div", "÷"),
        ("\\leq", "≤"), ("\\geq", "≥"), ("\\neq", "≠"), ("\\approx", "≈"),
        ("\\equiv", "≡"), ("\\rightarrow", "→"), ("\\leftarrow", "←"),
        ("\\Rightarrow", "⇒"), ("\\Leftrightarrow", "⇔"), ("\\in", "∈"),
        ("\\notin", "∉"), ("\\subseteq", "⊆"), ("\\supseteq", "⊇"),
        ("\\subset", "⊂"), ("\\cup", "∪"), ("\\cap", "∩"),
        ("\\sqrt", "√"), ("\\partial", "∂"), ("\\nabla", "∇"),
        ("\\ldots", "…"), ("\\cdots", "⋯"),
        ("\\zeta", "ζ"), ("\\eta", "η"), ("\\psi", "ψ"), ("\\Psi", "Ψ"),
        ("\\forall", "∀"), ("\\exists", "∃"), ("\\emptyset", "∅"),
        ("\\mathbb{R}", "ℝ"), ("\\mathbb{N}", "ℕ"), ("\\mathbb{Z}", "ℤ"),
        ("\\mathbb{Q}", "ℚ"), ("\\mathbb{C}", "ℂ"),
        ("\\ell", "ℓ"), ("\\hbar", "ℏ"), ("\\perp", "⊥"), ("\\parallel", "∥"),
        ("\\sin", "sin"), ("\\cos", "cos"), ("\\tan", "tan"), ("\\log", "log"),
        ("\\to", "→"), ("\\implies", "⇒"), ("\\iff", "⇔"),
        ("\\rho", "ρ"), ("\\tau", "τ"), ("\\chi", "χ"), ("\\xi", "ξ"),
        ("\\Phi", "Φ"), ("\\kappa", "κ"), ("\\nu", "ν"),
        ("\\ll", "≪"), ("\\gg", "≫"), ("\\circ", "∘"), ("\\degree", "°"),
        ("\\wedge", "∧"), ("\\vee", "∨"), ("\\otimes", "⊗"), ("\\oplus", "⊕"),
        ("\\therefore", "∴"), ("\\because", "∵"), ("\\angle", "∠"),
        ("\\sim", "∼"), ("\\propto", "∝"),
        ("\\iota", "ι"), ("\\upsilon", "υ"), ("\\varphi", "φ"), ("\\vartheta", "ϑ"),
        ("\\leftrightarrow", "↔"), ("\\uparrow", "↑"), ("\\downarrow", "↓"),
        ("\\cong", "≅"), ("\\mapsto", "↦"), ("\\neg", "¬"),
        ("\\nsubseteq", "⊈"), ("\\ast", "∗"), ("\\star", "⋆")
    ];

    private static readonly IReadOnlyDictionary<string, string> Replacements = Tokens.ToDictionary(token => token.From, token => token.To);

    public static string ToDisplay(string expression)
    {
        expression ??= string.Empty;
        var result = expression.Trim().Trim('$');
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\\frac\{([^{}]+)\}\{([^{}]+)\}",
            "($1)/($2)");
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\\sqrt\{([^{}]+)\}",
            "√($1)");
        result = Command.Replace(result, match => Replacements.GetValueOrDefault(match.Value) ?? match.Value);

        result = result.Replace("\\{", "{", StringComparison.Ordinal)
            .Replace("\\}", "}", StringComparison.Ordinal)
            .Replace("\\\\", "\n", StringComparison.Ordinal);
        return result;
    }
}
