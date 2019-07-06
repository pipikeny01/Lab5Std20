using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Operations;
using Newtonsoft.Json;

namespace Std20EasyArchitect.ApiHostBase.Package
{
    /// <summary>
    ///
    /// </summary>
    [Route("api/[controller]/{dllName}/{nameSpace}/{className}/{methodName}/{*pathInfo}")]
    [Route("api/[controller]/{dllName}/{nameSpace}/{methodName}/{*pathInfo}")]
    [Route("api/[controller]/{dllName}/{methodName}/{*pathInfo}")]
    [Route("api/[controller]/{methodName}/{*pathInfo}")]
    [Route("api/[controller]/{*pathInfo}")]
    [ApiController]
    public class ApiHostBase : ControllerBase
    {
        public ActionResult<object> Get(string dllName, string nameSpace, string className, string methodName)
        {
            if (string.IsNullOrEmpty(dllName)
                || string.IsNullOrEmpty(nameSpace)
                || string.IsNullOrEmpty(className)
                || string.IsNullOrEmpty(methodName))
            {
                return GetJsonMessage("參數錯誤!");
            }

            object result = null;

            var assemblyTarget = Assembly.Load($"{dllName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var targetBOType = assemblyTarget.GetType($"{nameSpace}.{className}");
            var targetBOIns = Activator.CreateInstance(targetBOType);

            var method = targetBOType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default)
                .FirstOrDefault(p => p.Name.ToLower() == methodName.ToLower());

            if (method == null)
            {
                return GetJsonMessage($"call method {methodName} 不存在 , 請確認!");
            }

            var queryCollection = Request.Query;
            if (queryCollection.Count > 0)
            {
                ParameterInfo[] methodParameters = method.GetParameters();
                Type parameterType = methodParameters[0].ParameterType;
                var parameterIns = Activator.CreateInstance(parameterType);

                if (methodParameters.Length > 0)
                {
                    foreach (var q in queryCollection)
                    {
                        var keyName = q.Key;
                        var keyValue = q.Value.ToString();

                        var parameterPropInfo =
                            parameterType
                                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default)
                                .FirstOrDefault(p => p.Name.ToLower() == keyName.ToLower());

                        if (parameterPropInfo != null)
                        {
                            parameterPropInfo.SetValue(parameterIns,
                                Convert.ChangeType(keyValue, parameterPropInfo.PropertyType));
                        }
                    }
                }

                result = method.Invoke(targetBOIns, new object[] { parameterIns });
            }
            else
            {
                result = method.Invoke(targetBOIns, null);
            }

            return result;
        }

        /// <summary>
        /// ApiHostBase 核心所提供的共用的 Post 方法
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="nameSpace"></param>
        /// <param name="className"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult<object> Post(
            string dllName,
            string nameSpace,
            string className,
            string methodName)
        {
            object result = null;

            if (string.IsNullOrEmpty(dllName) ||
                string.IsNullOrEmpty(nameSpace) ||
                string.IsNullOrEmpty(className) ||
                string.IsNullOrEmpty(methodName))
            {
                return GetJsonMessage("傳入的 Url 有誤！請確認呼叫 Api 的 Url 的格式！");
            }

            object parameter = GetParameter();

            Assembly assem = Assembly.Load($"{dllName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (assem != null)
            {
                Type runtimeType = assem.GetType($"{nameSpace}.{className}");
                object targetObj = Activator.CreateInstance(runtimeType);
                object invokeObj = null;
                MethodInfo[] methods = runtimeType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default);

                bool found = false;
                foreach (var method in methods)
                {
                    if (method.Name.ToLower() == methodName.ToLower())
                    {
                        ParameterInfo[] parames = method.GetParameters();
                        if (parames.Length > 0)
                        {
                            string paramName = parames[0].Name;
                            Type propertyType = parames[0].GetType();
                            Type parameType = parames[0].ParameterType;

                            if (parameType.IsValueType)
                            {
                                invokeObj = Convert.ChangeType(ReleaseStartEndQuotes(parameter.ToString()), propertyType);
                            }
                            else if (parameType.ToString() == "System.Byte[]")
                            {
                                if (parameter is Stream content)
                                {
                                    invokeObj = BinaryReadToEnd(content);
                                }
                                else
                                {
                                    invokeObj = JsonConvert.DeserializeObject(parameter.ToString(), parameType);
                                }
                            }
                            else
                            {
                                //如果都不是以上的物件，才進行 JSON DeserializeObject.
                                invokeObj = JsonConvert.DeserializeObject(parameter.ToString(), parameType);
                            }

                            result = method.Invoke(targetObj, new object[] { invokeObj });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 取得 HTTP Body 參數內容方法
        /// </summary>
        /// <returns></returns>
        private object GetParameter()
        {
            object inputStramStr = null;
            var ContentType = HttpContext.Request.ContentType;

            if (!string.IsNullOrEmpty(ContentType)
                && (ContentType.IndexOf("application/json") >= 0 || ContentType.IndexOf("text/plain") >= 0))
            {
                MemoryStream ms = new MemoryStream();
                HttpContext.Request.Body.CopyTo(ms);

                using (StreamReader inputStream = new StreamReader(ms, Encoding.UTF8))
                {
                    inputStramStr = Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            else
            {
                inputStramStr = HttpContext.Request.Body;
            }

            return inputStramStr;
        }

        /// <summary>
        /// 去除 Raw Data 內單一參數值 JSON 的雙引號.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private string ReleaseStartEndQuotes(string parameter)
        {
            parameter = parameter.StartsWith("\"") ? parameter.Substring(1, parameter.Length - 1) : parameter;
            parameter = parameter.EndsWith("\"") ? parameter.Substring(0, parameter.Length - 1) : parameter;
            return parameter;
        }

        private static ActionResult<object> GetJsonMessage(string message)
        {
            return new string[] { message }
                .Select(p => new { Err = p }).ToList();
        }

        #region The binary read method.

        /// <summary>
        /// The binary read method.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private byte[] BinaryReadToEnd(Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        #endregion The binary read method.
    }
}