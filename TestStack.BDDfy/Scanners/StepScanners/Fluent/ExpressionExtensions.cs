// Copyright (C) 2011, Mehdi Khalili
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the <organization> nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace TestStack.BDDfy.Scanners.StepScanners.Fluent
{
    public static class ExpressionExtensions
    {
        public static IEnumerable<object> ExtractConstants<T>(this Expression<Action<T>> expression)
        {
            var lambdaExpression = expression as LambdaExpression;
            if (lambdaExpression == null)
                throw new InvalidOperationException("Please provide a lambda expression.");

            var methodCallExpression = lambdaExpression.Body as MethodCallExpression;
            if(methodCallExpression == null)
                throw new InvalidOperationException("Please provide a *method call* lambda expression.");

            return ExtractConstants(methodCallExpression);
        }

        private static IEnumerable<object> ExtractConstants(Expression expression)
        {
            if (expression == null || expression is ParameterExpression)
                return new object[0];

            var memberExpression = expression as MemberExpression;
            if (memberExpression != null)
                return ExtractConstants(memberExpression);

            var constantExpression = expression as ConstantExpression;
            if (constantExpression != null)
                return ExtractConstants(constantExpression);

            var newArrayExpression = expression as NewArrayExpression;
            if (newArrayExpression != null)
                return ExtractConstants(newArrayExpression);

            var newExpression = expression as NewExpression;
            if (newExpression != null)
                return ExtractConstants(newExpression);

            var unaryExpression = expression as UnaryExpression;
            if (unaryExpression != null)
                return ExtractConstants(unaryExpression);

            return new object[0];
        }

        private static IEnumerable<object> ExtractConstants(MethodCallExpression methodCallExpression)
        {
            var constants = new List<object>();
            foreach (var arg in methodCallExpression.Arguments)
            {
                constants.AddRange(ExtractConstants(arg));
            }

            constants.AddRange(ExtractConstants(methodCallExpression.Object));

            return constants;
        }

        private static IEnumerable<object> ExtractConstants(UnaryExpression unaryExpression)
        {
            return ExtractConstants(unaryExpression.Operand);
        }

        private static IEnumerable<object> ExtractConstants(NewExpression newExpression)
        {
            var arguments = new List<object>();
            foreach (var argumentExpression in newExpression.Arguments)
            {
                arguments.AddRange(ExtractConstants(argumentExpression));
            }

            yield return newExpression.Constructor.Invoke(arguments.ToArray());
        }

        private static IEnumerable<object> ExtractConstants(NewArrayExpression newArrayExpression)
        {
            Type type = newArrayExpression.Type.GetElementType();
            if (type is IConvertible)
                return ExtractConvertibleTypeArrayConstants(newArrayExpression, type);
            
            return ExtractNonConvertibleArrayConstants(newArrayExpression, type);
        }

        private static IEnumerable<object> ExtractNonConvertibleArrayConstants(NewArrayExpression newArrayExpression, Type type)
        {
            var arrayElements = CreateList(type);
            foreach (var arrayElementExpression in newArrayExpression.Expressions)
            {
                object arrayElement;

                if (arrayElementExpression is ConstantExpression)
                    arrayElement = ((ConstantExpression)arrayElementExpression).Value;
                else
                    arrayElement = ExtractConstants(arrayElementExpression).ToArray();

                if (arrayElement is object[])
                {
                    foreach (var item in (object[])arrayElement)
                        arrayElements.Add(item);
                }
                else
                    arrayElements.Add(arrayElement);
            }

            return ToArray(arrayElements);
        }

        private static IEnumerable<object> ToArray(IList list)
        {
            var toArrayMethod = list.GetType().GetMethod("ToArray");
            yield return toArrayMethod.Invoke(list, new Type[] { });
        }

        private static IList CreateList(Type type)
        {
            return (IList)typeof(List<>).MakeGenericType(type).GetConstructor(new Type[0]).Invoke(BindingFlags.CreateInstance, null, null, null);
        }

        private static IEnumerable<object> ExtractConvertibleTypeArrayConstants(NewArrayExpression newArrayExpression, Type type)
        {
            var arrayElements = CreateList(type);
            foreach (var arrayElementExpression in newArrayExpression.Expressions)
            {
                var arrayElement = ((ConstantExpression)arrayElementExpression).Value;
                arrayElements.Add(Convert.ChangeType(arrayElement, arrayElementExpression.Type, null));
            }

            yield return ToArray(arrayElements);
        }

        private static IEnumerable<object> ExtractConstants(ConstantExpression constantExpression)
        {
            var constants = new List<object>();

            if (constantExpression.Value is Expression)
            {
                constants.AddRange(ExtractConstants((Expression)constantExpression.Value));
            }
            else
            {
                if (constantExpression.Type == typeof(string) ||
                    constantExpression.Type.IsPrimitive ||
                    constantExpression.Type.IsEnum ||
                    constantExpression.Value == null)
                    constants.Add(constantExpression.Value);
            }

            return constants;
        }

        private static IEnumerable<object> ExtractConstants(MemberExpression memberExpression)
        {
            var compiled = Expression.Lambda(memberExpression).Compile();
            var result = compiled.DynamicInvoke();
            return new[] { result };
        }
    }
}