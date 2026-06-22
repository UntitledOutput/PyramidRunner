using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using MyBox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace External
{
    public static class BaseUtils
    {
        public enum Type
        {
            Mammal,
            Reptilian,
            Aerial,
            Stellar,
            Insectoid,
            Amphibian,
            Crustacean,
        }
    
        public enum MoveType
        {
            Physical,
            Special,
            Status
        }
    
        public enum StatType
        {
            Health,
            Attack,
            Defense,
            Speed
        }
    
        public enum StatTarget
        {
            Enemy,
            Self
        }

        [Serializable]
        public class WeightedElement<T>
        {
            public T value;
            public float weight;
        }

        [Serializable]
        public class WeightedList<T>
        {
            public void Add(T value, float weight)
            {
                list.Add(new WeightedElement<T>{value =value, weight = weight});
            }

            public T GetRandom()
            {
                var weights = list.Select((element => element.weight)).ToList();

                var i = BaseUtils.WeightedRandom(weights);

                return list[i].value;
            }

            [SerializeField] private List<WeightedElement<T>> list;
        }
        
        static Vector3[] _corners = new Vector3[4];
    
        
        //Returns 'true' if we touched or hovering on Unity UI element.
        public static bool IsPointerOverUIElement()
        {
        
            var eventSystemRaysastResults = GetEventSystemRaycastResults();
            for (int index = 0; index < eventSystemRaysastResults.Count; index++)
            {
                RaycastResult curRaysastResult = eventSystemRaysastResults[index];
                if (curRaysastResult.gameObject.layer == LayerMask.NameToLayer("UI"))
                    return true;
            }
            return false;
        }
    
        public static bool CalculateLaunchAngle(Vector2 start, Vector2 target, float speed, out Vector2 launchVelocity)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y);
            Vector2 delta = target - start;
            float dx = delta.x;
            float dy = delta.y;

            float speedSq = speed * speed;
            float underSqrt = (speedSq * speedSq) - gravity * (gravity * dx * dx + 2 * dy * speedSq);

            if (underSqrt < 0)
            {
                launchVelocity = Vector2.zero;
                return false; // target unreachable
            }

            float sqrt = Mathf.Sqrt(underSqrt);

            float lowAngle = Mathf.Atan((speedSq - sqrt) / (gravity * dx));
            // (or use highAngle for a higher arc)

            float angle = lowAngle;
            launchVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            if (dx < 0)
                launchVelocity.x = -launchVelocity.x;

            return true;
        }

        public static Vector2 CalculateLinearVelocity(Vector2 start, Vector2 target, float timeToTarget)
        {
            Vector2 delta = target - start;
            float gravity = Physics2D.gravity.y;

            float vx = delta.x / timeToTarget;
            float vy = (delta.y - 0.5f * gravity * timeToTarget * timeToTarget) / timeToTarget;

            return new Vector2(vx, vy);
        }

        public static void EnsureParentDirectoryExists(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

    
        public static float RoundToNearest(this float value, float roundTo)
        {
            return Mathf.Round(value / roundTo) * roundTo;
        }
        
    
        public static Bounds TransformBoundsTo(this RectTransform source, Transform target)
        {
            // Based on code in ScrollRect's internal GetBounds and InternalGetBounds methods
            var bounds = new Bounds();
            if (source != null) {
                source.GetWorldCorners(_corners);

                var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                var matrix = target.worldToLocalMatrix;
                for (int j = 0; j < 4; j++) {
                    Vector3 v = matrix.MultiplyPoint3x4(_corners[j]);
                    vMin = Vector3.Min(v, vMin);
                    vMax = Vector3.Max(v, vMax);
                }

                bounds = new Bounds(vMin, Vector3.zero);
                bounds.Encapsulate(vMax);
            }
            return bounds;
        }

        /// <summary>
        /// Normalize a distance to be used in verticalNormalizedPosition or horizontalNormalizedPosition.
        /// </summary>
        /// <param name="axis">Scroll axis, 0 = horizontal, 1 = vertical</param>
        /// <param name="distance">The distance in the scroll rect's view's coordiante space</param>
        /// <returns>The normalized scoll distance</returns>
        public static float NormalizeScrollDistance(this ScrollRect scrollRect, int axis, float distance)
        {
            // Based on code in ScrollRect's internal SetNormalizedPosition method
            var viewport = scrollRect.viewport;
            var viewRect = viewport != null ? viewport : scrollRect.GetComponent<RectTransform>();
            var viewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);

            var content = scrollRect.content;
            var contentBounds = content != null ? content.TransformBoundsTo(viewRect) : new Bounds();

            var hiddenLength = contentBounds.size[axis] - viewBounds.size[axis];
            return distance / hiddenLength;
        }
    
        public static void ScrollToCenterX(this ScrollRect scrollRect, RectTransform target)
        {
            // The scroll rect's view's space is used to calculate scroll position
            var view = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            // Calcualte the scroll offset in the view's space
            var viewRect = view.rect;
            var elementBounds = target.TransformBoundsTo(view);
            var offset = viewRect.center.x - elementBounds.center.x;

            // Normalize and apply the calculated offset
            var scrollPos = scrollRect.horizontalNormalizedPosition - scrollRect.NormalizeScrollDistance(1, offset);
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp(scrollPos, 0f, 1f);
        }
    
        public static void ScrollToCenterY(this ScrollRect scrollRect, RectTransform target)
        {
            // The scroll rect's view's space is used to calculate scroll position
            var view = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            // Calcualte the scroll offset in the view's space
            var viewRect = view.rect;
            var elementBounds = target.TransformBoundsTo(view);
            var offset = viewRect.center.y - elementBounds.center.y;

            // Normalize and apply the calculated offset
            var scrollPos = scrollRect.verticalNormalizedPosition - scrollRect.NormalizeScrollDistance(1, offset);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp(scrollPos, 0f, 1f);
        }

        public static void LerpFloat(this Animator animator,int id, float value,float t)
        {
            animator.SetFloat(id,Mathf.Lerp(animator.GetFloat(id),value,t));
        }

        public static Color ColorFromHex(string hex)
        {
            Color color = Color.white;

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (ColorUtility.TryParseHtmlString(hex,out color))
            {
                
            }

            return color;
        }

        
        public static List<T> RemoveDuplicates<T>(this List<T> list)
        {
            var duplic = new List<T>();

            foreach (var x1 in list)
            {
                if (!duplic.Contains(x1))
                    duplic.Add(x1);
            }

            return duplic;
        }
        
        public static int WeightedRandom(List<float> chancesList)
        {
            int total = 0;
        
            foreach (float chance in chancesList) {
                total += (int)(chance * 10000f);
            }


            float x = (Random.Range(0,total))/10000f;
		
            int j = 0;
            float chances = 0;
		
            //Debug.Log(x);
		
            foreach (float chance in chancesList) {
                chances += chance;
                if (x < chances) {
                    break;
                }
                j++;
            }

            return j;
        }

        public static Vector3 Randomize(Vector3 min, Vector3 max)
        {
            var x = Random.Range(min.x, max.x);
            var y = Random.Range(min.y, max.y);
            var z = Random.Range(min.z, max.z);

            return new Vector3(x, y, z);
        }

        public static string Capitalize(this string word)
        {
            if(word.Length == 0)
            {
                return word;
            }      
            if(word.Length < 2)
            {
                return word.ToUpper();
            }
            return string.Concat(word[0].ToString().ToUpper(), word.Substring(1));
        }
    
        public class FlagDictionary : Dictionary<string,string>
        {
            public new void Add(string k, string v)
            {
                Debug.Log($"Adding new flag: {k}:{v}");
            
                base.Add(k,v);
            }
        
        
        
        }

        public static void SetLeft(this RectTransform rt, float left)
        {
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
        }

        public static void SetRight(this RectTransform rt, float right)
        {
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
        }

        public static void SetTop(this RectTransform rt, float top)
        {
            rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
        }

        public static void SetBottom(this RectTransform rt, float bottom)
        {
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
        }
    
        public static float Abs(this float f) => Mathf.Abs(f);

        public static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        public static void CopyTransforms(this Transform t, Transform target)
        {
            t.position = target.position;
            t.eulerAngles = target.eulerAngles;
            t.localScale = target.localScale;
        }

        public static Vector3 Slerp(this Vector3 v, Vector3 o, float t)
        {
            return Vector3.Slerp(v, o, t); // e
        }
        
        public static T[,] Trim2DArray<T>(this T[,] original) where T : class
        {
            int rows = original.GetLength(0);
            int cols = original.GetLength(1);

            int minRow = rows, maxRow = -1;
            int minCol = cols, maxCol = -1;

            // Step 1: Find bounds with non-null content
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (original[row, col] != null)
                    {
                        if (row < minRow) minRow = row;
                        if (row > maxRow) maxRow = row;
                        if (col < minCol) minCol = col;
                        if (col > maxCol) maxCol = col;
                    }
                }
            }

            // Nothing non-null
            if (maxRow == -1 || maxCol == -1)
                return new T[0, 0];

            int trimmedRows = maxRow - minRow + 1;
            int trimmedCols = maxCol - minCol + 1;
            var result = new T[trimmedRows, trimmedCols];

            // Step 2: Copy to new array
            for (int row = 0; row < trimmedRows; row++)
            {
                for (int col = 0; col < trimmedCols; col++)
                {
                    result[row, col] = original[minRow + row, minCol + col];
                }
            }

            return result;
        }

    
        public static T DeepClone<T>(this T obj)
        {
            using (var ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (T)formatter.Deserialize(ms);
            }
        }
    
        public static IEnumerator Coroutine(Action action)
        {
            action();
            yield return action;
        }

        [Obsolete("Obsolete")]
        public static Object FindObjectOfThisType(this Object obj)
        {
            return Object.FindObjectOfType(obj.GetType());
        }



        public static float Floor(this float f) => Mathf.Floor(f);
    
        public static Vector3 Midpoint(this Vector3 v, Vector3 o)
        {
            float Mid(float x1, float x2)
            {
                return (x1 + x2) / 2;
            }

            return new Vector3(Mid(v.x, o.x), Mid(v.y, o.y), Mid(v.z, o.z));

        }


        public static Bounds GetBounds(this GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(obj.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
            {
                bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

    
        public static float Pow(this float f, float p) => Mathf.Pow(f, p);
        public static int Pow(this int f, int p) => (int)Mathf.Pow(f, p);
        public static float Dot(this Vector3 v, Vector3 v2) => Vector3.Dot(v, v2);

        public static Vector3 RandonPointOnSurfaceBox(this Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            Vector3[] edges = new Vector3[24]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };

            var random = new System.Random();
        
            int edgeIndex = random.Next(0, 12) * 2; // Randomly select an edge
            Vector3 start = edges[edgeIndex];
            Vector3 end = edges[edgeIndex + 1];
        
            float t = (float)random.NextDouble(); // Random value between 0 and 1

            Vector3 randomPointOnEdge = new Vector3(
                start.x + t * (end.x - start.x),
                start.y + t * (end.y - start.y),
                start.z + t * (end.z - start.z)
            );

            return randomPointOnEdge;
        }

        public static string ReadStringUTF16(this BinaryReader r, int length)
        {
            // Calculate the total number of bytes to read
            int byteCount = length * 2;
            
            // Read the specified number of bytes
            byte[] stringBytes = r.ReadBytes(byteCount);

            // Convert the byte array to a UTF-16 string
            string result = Encoding.Unicode.GetString(stringBytes);
            
            // Trim any null terminator characters
            return result.TrimEnd('\0');
        }
    
        public static List<RaycastResult> GetEventSystemRaycastResults()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Mouse.current.position.value;
            List<RaycastResult> raysastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raysastResults);
            return raysastResults;
        }
        
        public static Tuple<int, int> CoordinatesOf<T>(this T[,] matrix, T value)
        {
            int w = matrix.GetLength(0); // width
            int h = matrix.GetLength(1); // height

            for (int x = 0; x < w; ++x)
            {
                for (int y = 0; y < h; ++y)
                {
                    if (value.Equals(matrix[x,y]))
                        return Tuple.Create(x, y);
                }
            }

            return Tuple.Create(-1, -1);
        }

        public static Color SetAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    
        public static float NearestRound(this float x, float delX)
        {
            if (delX < 1)
            {
                float i = (float)Mathf.Floor(x);
                float x2 = i;
                while ((x2 += delX) < x) ;
                float x1 = x2 - delX;
                return (Mathf.Abs(x - x1) < Mathf.Abs(x - x2)) ? x1 : x2;
            }
            else {
                return (float)Math.Round(x / delX, MidpointRounding.AwayFromZero) * delX;
            }
        }

        public static float InverseLerp(float a, float b, float value)
        {
            return Mathf.Clamp01((value - a) / (b - a));
        }

        public static float SmoothStep(float a, float b, float t)
        {
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t); // Hermite smoothstep
            return a + (b - a) * t;
        }

        public static void SetGlobalScale (this Transform transform, Vector3 globalScale)
        {
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3 (globalScale.x/transform.lossyScale.x, globalScale.y/transform.lossyScale.y, globalScale.z/transform.lossyScale.z);
        }

        public static void RemoveAllChildrenExcept(this Transform transform, string excludedChild)
        {
            // Iterate in reverse order to avoid issues when destroying elements
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                // Get the child Transform
                Transform child = transform.GetChild(i);
                
                if (child.name == excludedChild)
                    continue;

                // Destroy the GameObject associated with the child Transform
                Object.Destroy(child.gameObject);
            }
        }

        public static void RemoveAllChildrenExcept(this Transform transform, List<string> excludedChilds)
        {
            // Iterate in reverse order to avoid issues when destroying elements
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                // Get the child Transform
                Transform child = transform.GetChild(i);
                
                if (excludedChilds.Contains(child.name))
                    continue;

                // Destroy the GameObject associated with the child Transform
                Object.Destroy(child.gameObject);
            }
        }
        
        public static void RemoveAllChildrenIf(this Transform transform, string excludedChild)
        {
            // Iterate in reverse order to avoid issues when destroying elements
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                // Get the child Transform
                Transform child = transform.GetChild(i);
                
                if (child.name != excludedChild)
                    continue;

                // Destroy the GameObject associated with the child Transform
                Object.Destroy(child.gameObject);
            }
        }
        
        public static void RemoveAllChildrenIf(this Transform transform, List<string> includedChilds)
        {
            // Iterate in reverse order to avoid issues when destroying elements
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                // Get the child Transform
                Transform child = transform.GetChild(i);
                
                if (!includedChilds.Contains(child.name))
                    continue;

                // Destroy the GameObject associated with the child Transform
                Object.Destroy(child.gameObject);
            }
        }
        
        public static void RemoveAllChildren(this Transform transform)
        {
            if (transform.childCount > 0)
            {
                // Iterate in reverse order to avoid issues when destroying elements
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    // Get the child Transform
                    Transform child = transform.GetChild(i);

                    // Destroy the GameObject associated with the child Transform
                    Object.Destroy(child.gameObject);
                }
            }
        }

        public static void SetActiveFast(this GameObject g, bool v)
        {
            if (g.activeSelf != v)
                g.SetActive(v);
        }

        public static List<Transform> GetAllChildren(this Transform transform)
        {
            List<Transform> children = new List<Transform>();

            foreach (Transform o in transform)
            {
                children.Add(o);
            }
            
            return children;
        }
        public static List<Transform> RecursiveGetAllChildren(this Transform transform)
        {
            List<Transform> children = new List<Transform>();

            foreach (Transform o in transform)
            {
                children.Add(o);
                if (o.childCount > 0)
                    children.AddRange(o.RecursiveGetAllChildren());
            }
            
            return children;
        }
        
        public static Transform RecursiveFind(string name, bool contains = true)
        {
            var objs = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var gameObject in objs)
            {
                //Debug.Log(gameObject.name);
                var c = gameObject.transform.RecursiveFind(name, contains);

                if (c)
                    return c;
            }

            return null;
        }
        
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text; // Search string not found
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        
        public static Transform RecursiveFindChild(Transform parent, string childName, bool contains = true)
        {
            foreach (Transform child in parent)
            {

                bool b = child.name.Contains(childName);
                if (!contains)
                    b = child.name == childName;
                if(b)
                {
                    return child;
                }
                else
                {
                    Transform found = RecursiveFindChild(child, childName,contains);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        public static Transform RecursiveFind(this Transform t, string childName, bool contains = true) =>
            RecursiveFindChild(t, childName, contains);
    
        public static GameObject RecursiveFind(this GameObject t, string childName, bool contains = true) =>
            RecursiveFindChild(t.transform, childName, contains).gameObject;
        
        public static Transform RecursiveFind(this MonoBehaviour t, string childName, bool contains = true) =>
            RecursiveFindChild(t.transform, childName, contains);
        
        public static T RecursiveFind<T>(this GameObject t, string childName, bool contains = true) =>
            RecursiveFindChild(t.transform, childName, contains).GetComponent<T>();
        
        public static T RecursiveFind<T>(this Transform t, string childName, bool contains = true) =>
            RecursiveFindChild(t, childName, contains).GetComponent<T>();

        public static float[] ToArray(this Vector3 v)
        {
            return new float[]{v.x,v.y,v.z};
        }
        public static float[] ToArray(this Quaternion v)
        {
            return new float[]{v.x,v.y,v.z,v.w};
        }
    }
}
