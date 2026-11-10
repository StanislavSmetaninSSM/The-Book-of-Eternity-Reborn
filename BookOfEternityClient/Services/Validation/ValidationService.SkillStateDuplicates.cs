using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains first row coordinates only for one collection validation call. This is not a
    /// reference authority or cross-operation cache; different collections never share it.
    /// </summary>
    private sealed class ExactSkillStateRows
    {
        private readonly Dictionary<string, Dictionary<string, string>> _firstPathsByOwner =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _trimSkillNames;
        private readonly string _section;

        /// <summary>Creates a fresh collection-local duplicate tracker.</summary>
        /// <param name="trimSkillNames">Preserves the collection's existing canonical name-reader semantics.</param>
        /// <param name="section">The active, passive or mastery diagnostic section.</param>
        internal ExactSkillStateRows(bool trimSkillNames, string section)
        {
            _trimSkillNames = trimSkillNames;
            _section = section;
        }

        /// <summary>Reports a later exact-name occurrence without choosing or modifying either row.</summary>
        /// <param name="row">A skill or mastery object already checked by the collection validator.</param>
        /// <param name="rowPath">The complete indexed coordinate of this row.</param>
        /// <param name="owner">Existing actor key, or player; absent NPC identity has its own shape error.</param>
        /// <param name="issues">The current validation operation's diagnostics.</param>
        internal void Record(JsonElement row, string rowPath, string? owner, List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(owner) ||
                !row.TryGetProperty("skillName", out var nameNode) ||
                nameNode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(nameNode.GetString()))
            {
                return;
            }

            var name = nameNode.GetString()!;
            if (_trimSkillNames)
                name = name.Trim();
            if (!_firstPathsByOwner.TryGetValue(owner, out var firstPaths))
            {
                firstPaths = new Dictionary<string, string>(StringComparer.Ordinal);
                _firstPathsByOwner.Add(owner, firstPaths);
            }

            var fieldPath = rowPath + ".skillName";
            if (firstPaths.TryAdd(name, fieldPath))
                return;

            issues.Add(new ValidationIssue(
                fieldPath,
                IssueSeverity.Error,
                "Повторный точный skillName для одного владельца в одной коллекции навыков или mastery",
                code: "skill_state_duplicate_exact_name",
                actor: owner,
                section: _section,
                expected: $"unique exact skillName per owner and collection; first occurrence: {firstPaths[name]}",
                actual: name,
                repairHint: "Сверь обе записи с подтверждённым состоянием и источником прогрессии. " +
                    "Не выбирай победившую запись, не удаляй и не объединяй неоднозначную прогрессию автоматически; " +
                    "если источник не определяет единственное корректное состояние, останови исправление и запроси уточнение."));
        }
    }
}
