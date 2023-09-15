import { CanDeactivateFn } from '@angular/router';
import { MemberEditComponent } from '../members/member-edit/member-edit.component';

export const preventUnsavedChangesGuard: CanDeactivateFn<MemberEditComponent> = (component, currentRoute, currentState, nextState) => {
  if (component.editForm?.dirty) {
    return confirm("Are you sure want to leave? Any unsaved changes will be lost...");
  }
  
  return true;
};
