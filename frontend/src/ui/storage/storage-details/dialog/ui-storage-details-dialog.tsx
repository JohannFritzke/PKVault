import { CloseButton, Modal, useMatches } from '@mantine/core';
import type React from 'react';
import classes from './ui-storage-details-dialog.module.css';

type UIStorageDetailsDialogProps = {
    opened: boolean;
    onClose: () => void;
    children: React.ReactNode;
};

/** Pokémon details in a centered dialog (replaces the side panel for a single pokémon). */
export const UIStorageDetailsDialog: React.FC<UIStorageDetailsDialogProps> = ({ opened, onClose, children }) => {
    const fullScreen = useMatches({ base: true, sm: false });

    return <Modal
        opened={opened}
        onClose={onClose}
        keepMounted={false}
        withCloseButton={false}
        // Escape is the "back" control of the dialog content
        closeOnEscape={false}
        size={760}
        fullScreen={fullScreen}
        padding={0}
        styles={{
            // nested popovers (trade, release...) must not be cut by the dialog
            content: { overflow: 'visible' },
        }}
    >
        {children}
    </Modal>;
};

export type UIStorageDetailsDialogLayoutProps = {
    /** Receives the close button, shown at the end of the header. */
    header: (closeBtn: React.ReactNode) => React.ReactNode;
    main: React.ReactNode;
    notice?: React.ReactNode;
    /** Tabs, or the edit form. */
    body: React.ReactNode;
    footer: React.ReactNode;
    onClose: () => void;
};

export const UIStorageDetailsDialogLayout: React.FC<UIStorageDetailsDialogLayoutProps> = ({ header, main, notice, body, footer, onClose }) => {
    return <div className={classes.root}>
        <div className={classes.header}>{header(<CloseButton onClick={onClose} />)}</div>
        <div className={classes.main}>{main}</div>
        {notice && <div className={classes.notice}>{notice}</div>}
        <div className={classes.body}>{body}</div>
        <div className={classes.footer}>{footer}</div>
    </div>;
};
