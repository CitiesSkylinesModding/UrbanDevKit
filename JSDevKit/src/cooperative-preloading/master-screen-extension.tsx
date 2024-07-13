import { bindValue, useValue } from 'cs2/api';
import { getModule } from 'cs2/modding';
import type { Button, TooltipProps } from 'cs2/ui';
import React, {
    type ComponentType,
    type PropsWithChildren,
    type ReactElement,
    type ReactNode,
    useMemo
} from 'react';
import { addStylesheet } from '../internals/index.js';
import { style } from './master-screen-extension.css.js';

interface DescriptionTooltipProps extends Omit<TooltipProps, 'tooltip'> {
    title: string;
    description: string;
    content?: ReactNode;
}

/**
 * Interface received through the binding.
 */
interface PreloadingOperation {
    id: string;
    modName: string;
    operationName: string;
    state: 'None' | 'Pending' | 'Running' | 'Done' | 'Failed';
}

interface MasterScreenExtensionProps {
    /** Vanilla component to wrap. */
    readonly original: ComponentType<PropsWithChildren>;

    /** Original props passed to the vanilla component. */
    readonly props: PropsWithChildren;
}

/**
 * Creates the MasterScreen extension that disables menu buttons and displays
 * spinners when operations are in progress.
 * The function is a component factory that returns the actual component.
 * Since it is a feature singleton, it is used as the top level (instead of the
 * modules') for any initialization code and declaring other functions.
 */
export function createMasterScreenExtension(): ComponentType<MasterScreenExtensionProps> {
    // Retrieve balloon tooltip component, not exposed by cs2/ui.
    const DescriptionTooltip: React.FC<DescriptionTooltipProps> = getModule(
        'game-ui/common/tooltip/description-tooltip/description-tooltip.tsx',
        'DescriptionTooltip'
    );

    // Those are the buttons that will be disabled and their icon changed when
    // mods are preloading. Identifier is from a memo ID inside the original
    // component, could break if the basegame component structure changes, but
    // it's already slightly better than position, icon or localization-based
    // detection. If this breaks, switch to one of those solutions.
    const buttonsToPatch = new Set([
        'Menu.CONTINUE_GAME',
        'Menu.LOAD_GAME',
        'Menu.NEW_GAME'
    ]);

    // Patched buttons to check if all buttons were patched.
    const patchedButtons = new Set<string>();

    // Icons that were preloaded to avoid preloading them multiple times.
    const preloadedIcons = new Set<string>();

    addStylesheet('cooperative-preloading', style);

    const operations$ = bindValue<readonly PreloadingOperation[]>(
        'udk.cooperativePreloading',
        'operations',
        []
    );

    /**
     * The MasterScreen wrapper component.
     */
    return function MasterScreenExtension(
        props: MasterScreenExtensionProps
    ): ReactNode {
        const MasterScreen = props.original;

        const operations = useValue(operations$);

        const children = useMemo(
            () => findAndPatchButtons(props.props.children, operations),
            [props.props.children, operations]
        );

        const newProps = { ...props.props, children };

        return <MasterScreen {...newProps} />;
    };

    /**
     * Entry point for {@link findAndPatchButtons}, which from a JSX tree,
     * recursively scans the structure to find and patch buttons listed in
     * {@link buttonsToPatch}.
     * Returns a new JSX tree instance with the patched buttons, cloning the
     * modified elements is necessary because mutating the original tree does
     * not work well if at all, due to how React reconciliation works.
     */
    function findAndPatchButtons(
        originalChildren: ReactNode,
        operations: readonly PreloadingOperation[]
    ): ReactNode {
        // It is important to catch errors here as any failure would cause the
        // whole menu to break making the game unusable.
        try {
            const children = findAndPatchButtonsRecursive(
                originalChildren,
                operations
            );

            // Check if all buttons were patched, if not log an error, it's
            // probably that the basegame changed or a mod is interfering.
            if (
                children != originalChildren &&
                patchedButtons.size != buttonsToPatch.size
            ) {
                const missing = [...buttonsToPatch.values()]
                    .filter(name => !patchedButtons.has(name))
                    .join(', ');

                console.error(
                    `Unable to find or patch Main Menu Screen buttons ${missing}.`
                );
            }

            return children;
        } catch (err) {
            console.error(
                `Unable to find or patch Main Menu Screen buttons, either due to a mod interfering or a basegame change.`,
                err
            );

            return originalChildren;
        }
    }

    /**
     * @see findAndPatchButtons
     */
    function findAndPatchButtonsRecursive(
        children: ReactNode,
        operations: readonly PreloadingOperation[]
    ): ReactNode {
        const isAnyOperationInProgress = operations.some(
            op => op.state != 'Done' && op.state != 'Failed'
        );

        const isAnyOperationFailed = operations.some(
            op => op.state == 'Failed'
        );

        // If no operation is in progress or failed, return the children as is
        // as there is no change to be made.
        if (!(isAnyOperationInProgress || isAnyOperationFailed)) {
            return children;
        }

        return React.Children.map(children, element => {
            // Buttons we want to patch are React elements, if it's not a React
            // element, pass it through.
            if (!React.isValidElement<{ children?: ReactNode }>(element)) {
                return element;
            }

            const shouldBePatched = isButtonToPatch(element);

            if (shouldBePatched) {
                return patchButton(
                    element,
                    isAnyOperationInProgress,
                    isAnyOperationFailed
                );
            }

            // If the element is not a button to patch but has children,
            // recursively scan it to find our buttons.
            if (!shouldBePatched && element.props.children) {
                // Yes, cloning is necessary or React will not detect changes.
                return React.cloneElement(element, {
                    children: findAndPatchButtonsRecursive(
                        element.props.children,
                        operations
                    )
                });
            }

            // If the element is not a button to patch and has no children,
            // pass it through.
            return element;
        });
    }

    /**
     * Checks if the element is a React element and a button listed in
     * {@link buttonsToPatch}.
     */
    function isButtonToPatch(
        element: ReactElement
    ): element is ReactElement<React.ComponentProps<typeof Button>> {
        if (
            !(
                'onSelect' in element.props &&
                React.isValidElement(element.props.children) &&
                typeof element.props.children.type == 'object' &&
                'displayName' in element.props.children.type &&
                buttonsToPatch.has(element.props.children.type.displayName)
            )
        ) {
            return false;
        }

        // Mark the button as patched.
        patchedButtons.add(element.props.children.type.displayName);

        return true;
    }

    /**
     * Given a button element, patches it with a spinner or a warning icon and
     * disables it when any operation is in progress.
     */
    function patchButton(
        element: ReactElement,
        isAnyOperationInProgress: boolean,
        isAnyOperationFailed: boolean
    ): ReactElement {
        // We will replace the icon with a spinner, but restore the original
        // icon later. This causes a short glitch when the original image is
        // set again because it won't be loaded yet.
        // This mitigates the issue by leveraging the "browser" cache, although
        // not totally and some artifacts may still be visible.
        if (element.props.src && !preloadedIcons.has(element.props.src)) {
            // Mark the icon as preloaded to avoid instantiating the same img
            // each time the component is rendered.
            preloadedIcons.add(element.props.src);
            const img = new Image();
            img.src = element.props.src;
        }

        // New props to apply to the button element.
        const props: React.ComponentProps<typeof Button> = {};

        // When any operation is in progress, disable the button and show a
        // spinner icon.
        if (isAnyOperationInProgress) {
            props.disabled = true;
            props.src = 'Media/Glyphs/Progress.svg';
            props.theme = { ...element.props.theme };
            // biome-ignore lint/style/noNonNullAssertion: false positive
            props.theme!.icon += ' udk-cooperative-preloading-spin';

            return React.cloneElement(element, props);
        }

        // When any operation failed, disable the button and show a warning
        // icon, with a tooltip explaining the situation.
        if (isAnyOperationFailed) {
            props.src = 'Media/Glyphs/Warning.svg';
            props.theme = { ...element.props.theme };
            // biome-ignore lint/style/noNonNullAssertion: false positive
            props.theme!.icon += ' udk-cooperative-preloading-icon-failed';

            return (
                <DescriptionTooltip
                    title='Some mods failed to preload'
                    description='Check for error in notifications and logs. Continue only if you know what you’re doing.'
                    alignment='center'
                    direction='right'>
                    {React.cloneElement(element, props)}
                </DescriptionTooltip>
            );
        }

        return element;
    }
}
