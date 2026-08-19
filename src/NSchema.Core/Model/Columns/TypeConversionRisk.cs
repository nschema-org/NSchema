namespace NSchema.Model.Columns;

/// <summary>
/// The known risk of converting stored values between SQL types.
/// </summary>
public enum TypeConversionRisk
{
    /// <summary>
    /// The model knows no conversion failure is implied.
    /// </summary>
    Safe,

    /// <summary>
    /// Existing values can fail to convert.
    /// </summary>
    MayFail,

    /// <summary>
    /// The model does not know enough about the types to assess the conversion.
    /// </summary>
    Unknown,
}
