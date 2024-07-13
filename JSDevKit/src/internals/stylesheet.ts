import packageJson from '../../package.json';

/**
 * Add a stylesheet to the document by creating a new <style> element with the
 * specified CSS in the document head.
 * Call it only once when a feature is initialized.
 */
export function addStylesheet(featureName: string, styles: string): void {
    const id = `udk-${packageJson.version}-stylesheet-${featureName}`;
    if (document.getElementById(id)) {
        console.error(
            `Stylesheet for UDK v${packageJson.version}, feature "${featureName}" was already declared.`
        );
        return;
    }

    const styleElement = document.createElement('style');
    styleElement.id = id;
    styleElement.textContent = styles;

    document.head.appendChild(styleElement);
}
