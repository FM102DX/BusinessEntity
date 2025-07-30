window.TreeMultiSelect = {
    isCtrlPressed: false,
    isShiftPressed: false,
    
    initialize: function () {
        document.addEventListener('keydown', (e) => {
            this.isCtrlPressed = e.ctrlKey;
            this.isShiftPressed = e.shiftKey;
        });
        
        document.addEventListener('keyup', (e) => {
            this.isCtrlPressed = e.ctrlKey;
            this.isShiftPressed = e.shiftKey;
        });
        
        // Сброс состояния при потере фокуса окна
        window.addEventListener('blur', () => {
            this.isCtrlPressed = false;
            this.isShiftPressed = false;
        });
    },
    
    getKeyState: function () {
        return {
            ctrl: this.isCtrlPressed,
            shift: this.isShiftPressed
        };
    },
    
    // Принудительное обновление CSS-классов дерева
    forceRefreshTreeSelection: function () {
        try {
            // Находим все элементы с классом tree-node-selected
            const selectedElements = document.querySelectorAll('.rz-treenode-content-selected');
            console.log(`Found ${selectedElements.length} elements with rz-treenode-content-selected`);
            
            // Принудительно удаляем класс выделения
            selectedElements.forEach((element, index) => {
                console.log(`Removing rz-treenode-content-selected class from element ${index}: ${element.textContent}`);
                element.classList.remove('rz-treenode-content-selected');
                
                // Принудительно обновляем стили
                element.style.backgroundColor = '';
                element.style.color = '';
            });
            
            // Принудительно перерисовываем DOM
            document.body.offsetHeight; // Trigger reflow
            
            return selectedElements.length;
        } catch (error) {
            console.error('Error in forceRefreshTreeSelection:', error);
            return -1;
        }
    },
    
    // Переменная для хранения ссылки на всплывающий элемент
    dragTooltip: null,
    
    // Обработчик движения мыши для обновления позиции всплывающего элемента
    onMouseMove: function(e) {
        if (this.dragTooltip) {
            this.dragTooltip.style.left = (e.clientX + 10) + 'px';
            this.dragTooltip.style.top = (e.clientY + 10) + 'px';
        }
    }
};

// Функция для создания всплывающего элемента с именами перетаскиваемых элементов
window.createDragTooltip = function(content) {
    try {
        // Удаляем существующий tooltip, если есть
        window.removeDragTooltip();
        
        // Создаем новый элемент
        const tooltip = document.createElement('div');
        tooltip.id = 'drag-tooltip';
        tooltip.innerHTML = content;
        tooltip.style.cssText = `
            position: fixed;
            background: rgba(0, 0, 0, 0.8);
            color: white;
            padding: 8px 12px;
            border-radius: 4px;
            font-size: 12px;
            max-width: 200px;
            z-index: 10000;
            pointer-events: none;
            white-space: nowrap;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
        `;
        
        document.body.appendChild(tooltip);
        window.TreeMultiSelect.dragTooltip = tooltip;
        
        // Добавляем обработчик движения мыши
        document.addEventListener('mousemove', window.TreeMultiSelect.onMouseMove);
        
        console.log('Drag tooltip created with content:', content);
    } catch (error) {
        console.error('Error creating drag tooltip:', error);
    }
};

// Функция для обновления позиции всплывающего элемента
window.updateDragTooltipPosition = function(clientX, clientY) {
    try {
        const tooltip = window.TreeMultiSelect.dragTooltip;
        if (tooltip) {
            tooltip.style.left = (clientX + 10) + 'px';
            tooltip.style.top = (clientY + 10) + 'px';
        }
    } catch (error) {
        console.error('Error updating drag tooltip position:', error);
    }
};

// Функция для удаления всплывающего элемента
window.removeDragTooltip = function() {
    try {
        const tooltip = document.getElementById('drag-tooltip');
        if (tooltip) {
            tooltip.remove();
        }
        
        // Очищаем ссылку
        window.TreeMultiSelect.dragTooltip = null;
        
        // Удаляем обработчик движения мыши
        document.removeEventListener('mousemove', window.TreeMultiSelect.onMouseMove);
        
        console.log('Drag tooltip removed');
    } catch (error) {
        console.error('Error removing drag tooltip:', error);
    }
};