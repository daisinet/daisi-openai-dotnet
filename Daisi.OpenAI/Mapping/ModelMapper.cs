using Daisi.OpenAI.Models;
using Daisi.Protos.V1;

namespace Daisi.OpenAI.Mapping;

public static class ModelMapper
{
    public static ModelObject ToModelObject(AIModel aiModel)
    {
        return new ModelObject
        {
            Id = aiModel.Name,
            Object = "model",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            OwnedBy = "daisi"
        };
    }

    public static List<ModelObject> ToModelList(IEnumerable<AIModel> aiModels)
    {
        return aiModels
            .Where(m => m.Enabled)
            .Select(ToModelObject)
            .ToList();
    }
}
