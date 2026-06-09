using System;
using System.Collections.Generic;

[Serializable]
public class ExpressionMappingData
{
    public string emotion;
    public string facialExpression;
    public int actionParam;
}

[Serializable]
public class MappingListWrapper
{
    public List<ExpressionMappingData> mappings;
}
