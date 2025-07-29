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
            const selectedElements = document.querySelectorAll('.tree-node-selected');
            console.log(`Found ${selectedElements.length} elements with tree-node-selected class`);
            
            // Принудительно удаляем класс выделения
            selectedElements.forEach((element, index) => {
                console.log(`Removing tree-node-selected class from element ${index}: ${element.textContent}`);
                element.classList.remove('tree-node-selected');
                
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
    }
};