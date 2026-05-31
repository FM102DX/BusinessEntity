// Улучшенные нативные тултипы для навигации
document.addEventListener('DOMContentLoaded', function() {
    console.log('Tooltips initialized for navigation');
    
    // Дополнительная стилизация тултипов через CSS (опционально)
    var style = document.createElement('style');
    style.textContent = `
        /* Стилизация нативных тултипов */
        [title]:hover::after {
            content: attr(title);
            position: absolute;
            background: rgba(0,0,0,0.8);
            color: white;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            white-space: nowrap;
            z-index: 1000;
            pointer-events: none;
        }
    `;
    document.head.appendChild(style);
});
