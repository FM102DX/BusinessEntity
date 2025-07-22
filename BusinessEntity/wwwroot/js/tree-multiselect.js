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
    }
}; 