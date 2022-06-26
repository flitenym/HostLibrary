using System.Collections.Generic;
using System.Linq;

namespace HostLibrary.Core.Structs
{
    /// <summary>
    /// Модуль - узел графа
    /// </summary>
    public class Node
    {
        private readonly ModulesGraph _graph;

        private IEnumerable<Node> _dependantNodes;

        /// <summary>
        /// Объект модуля
        /// </summary>
        public InternalModule Module { get; }

        /// <summary>
        /// Список зависимых модулей
        /// </summary>
        public IEnumerable<Node> DependantNodes
        {
            get
            {
                if (_dependantNodes == null)
                {
                    _dependantNodes = _graph._edges.Where(e => e.From == Module.Metadata.Key).Select(e => new Node(_graph, _graph.First(m => m.Metadata.Key == e.To)));
                }

                return _dependantNodes;
            }
        }

        internal Node(ModulesGraph graph, InternalModule module)
        {
            _graph = graph;
            Module = module;
        }
    }
}
