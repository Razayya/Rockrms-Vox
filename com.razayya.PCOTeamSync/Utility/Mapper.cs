using System;
using System.Collections.Generic;

namespace com.razayya.PCOTeamSync.Utility
{
    public class Mapper<TNative, TForeign>
    {
        private Dictionary<TNative, TForeign> native = new Dictionary<TNative, TForeign>();
        private Dictionary<TForeign, TNative> foreign = new Dictionary<TForeign, TNative>();

        public void Add( TNative naitiveKey, TForeign foreignKey )
        {
            native.Add( naitiveKey, foreignKey );
            foreign.Add( foreignKey, naitiveKey );
        }
        public TForeign GetForeignKey( TNative nativeKey ) => native[nativeKey];

        public TNative GetNativeKey( TForeign foreignKey ) => foreign[foreignKey];

        public bool ContainsForeignKey( TNative nativeKey ) => native.ContainsKey( nativeKey );

        public bool ContainsNativeKey( TForeign foreignKey ) => foreign.ContainsKey( foreignKey );

    }

    public static class EnumerableExtensions
    {
        public static Mapper<TNative, TForeign> ToMapper<T, TNative, TForeign>(
            this IEnumerable<T> source,
            Func<T, TNative> nativeKeySelector,
            Func<T, TForeign> foreignKeySelector )
        {
            var mapper = new Mapper<TNative, TForeign>();

            foreach (var element in source)
            {
                var nativeKey = nativeKeySelector( element );
                var foreignKey = foreignKeySelector( element );
                mapper.Add( nativeKey, foreignKey );
            }

            return mapper;
        }
    }
}
