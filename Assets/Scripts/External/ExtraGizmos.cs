using UnityEngine;


    public static class ExtraGizmos
    {
        public static void DrawGizmosCircle(Vector3 pos, Vector3 normal, float radius, int numSegments)
        {
            Vector3 temp = (Mathf.Abs(normal.y) < 0.999f) ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(temp, normal).normalized;
            Vector3 forward = Vector3.Cross(normal, right).normalized;

            Vector3 rightScaled = right * radius;
            Vector3 forwardScaled = forward * radius;
    
            Vector3 prevPt = pos + forwardScaled;
            float angleStep = (Mathf.PI * 2f) / numSegments;
            int lastSegment = numSegments - 1;
    
            for (int i = 0; i < numSegments; i++)
            {
                float angle = (i == lastSegment) ? 0f : (i + 1) * angleStep;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
        
                Vector3 nextPt = pos + (rightScaled * sin) + (forwardScaled * cos);

                Gizmos.DrawLine(prevPt, nextPt);
                prevPt = nextPt;
            }
        }
    }