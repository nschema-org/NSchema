using System.Text.Json;

namespace NSchema.Model.Serialization;

/// <summary>
/// Composes the schema model's JSON conventions onto a consumer's own <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <param name="options">The options to add the converters to.</param>
    extension(JsonSerializerOptions options)
    {
        /// <summary>
        /// Registers the converters that render the model's value objects and addresses
        /// </summary>
        /// <returns>The same options, for chaining.</returns>
        public JsonSerializerOptions AddModelConverters()
        {
            ArgumentNullException.ThrowIfNull(options);

            options.Converters.Add(new ValueObjectJsonConverter());
            options.Converters.Add(new ObjectAddressJsonConverter());
            return options;
        }
    }
}
